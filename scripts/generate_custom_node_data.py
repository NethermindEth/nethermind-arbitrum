import os
import json
import gzip
import base64

from pathlib import Path
from jinja2 import Template
from jinja2_ansible_filters import AnsibleCoreFiltersExtension


# Constants
CUSTOM_NODE_DATA_FILE = Path("custom_node_data.json")
CUSTOM_NODE_NAME = "nethermind-arb"
CUSTOM_MACHINE_TYPE_PER_CHAIN = {"sepolia": "g6-linode-8"}

DEFAULT_TIMEOUT = 24
DEFAULT_CUSTOM_MACHINE_TYPE = "g6-linode-8"
DEFAULT_BLOCK_PROCESSING_TIMEOUT = 30


def get_nethermind_config(
    chain: str,
    nethermind_service_name: str,
    nethermind_image: str,
    nethermind_rpc_port: int,
    nethermind_engine_port: int,
    docker_network_name: str,
    block_processing_timeout: int = DEFAULT_BLOCK_PROCESSING_TIMEOUT,
    # TODO: Add more flags options as needed
) -> dict:
    # Paths
    nethermind_data_dir = "/app/nethermind_db"
    nethermind_host_data_dir = "nethermind-data"

    nethermind_command = []
    if chain == "sepolia":
        nethermind_command += [
            "-c",
            "arbitrum-sepolia-archive",
        ]
    # Add default flags
    nethermind_command += [
        "--data-dir",
        nethermind_data_dir,
        "--stylustarget-amd64",
        "x86_64-linux-unknown",
        "--JsonRpc.Host",
        "0.0.0.0",
        "--JsonRpc.EngineHost",
        "0.0.0.0",
        "--Arbitrum.BlockProcessingTimeout",
        str(block_processing_timeout),
        "--log",
        "debug",
    ]
    ## Add metrics flags
    nethermind_command += [
        "--Metrics.Enabled",
        "true",
        "--Metrics.ExposePort",
        "8008",
        "--Metrics.ExposeHost",
        "0.0.0.0",
        "--Metrics.PushGatewayUrl",
        "http://localhost:9091",  # TODO: update to use Nethermind prometheus pushgateway
    ]
    # Return config
    return {
        "image": nethermind_image,
        "container_name": nethermind_service_name,
        "ports": [
            f"{nethermind_rpc_port}:{nethermind_rpc_port}",
            f"{nethermind_engine_port}:{nethermind_engine_port}",
        ],
        "volumes": [f"{nethermind_host_data_dir}:{nethermind_data_dir}"],
        "environment": [
            "STYLUS_ARCH=amd64",
            "STYLUS_TARGET=x86_64-linux-unknown",
        ],
        "command": nethermind_command,
        "networks": [docker_network_name],
    }


def get_nitro_config(
    chain: str,
    nitro_service_name: str,
    nitro_image: str,
    nitro_nethermind_rpc_url: str,
    docker_network_name: str,
) -> dict:
    nitro_command = []
    if chain == "sepolia":
        nitro_command += [
            "--chain.id=421614",
            "--parent-chain.connection.url=http://209.127.228.66/rpc/6ekWpL9BXR0aLXrd",
            "--parent-chain.blob-client.beacon-url=http://209.127.228.66/consensus/6ekWpL9BXR0aLXrd",
        ]
    nitro_command += [
        "--persistent.global-config=/tmp/sequencer_follower.json",
        "--execution.forwarding-target null",
        "--execution.enable-prefetch-block=false",
    ]
    return {
        "image": nitro_image,
        "container_name": nitro_service_name,
        "ports": [
            # TODO: Add ports if needed
        ],
        "volumes": [
            # TODO: Add volumes if needed
        ],
        "environment": [
            "CGO_LDFLAGS=-Wl,-no_warn_duplicate_libraries",
            "PR_EXIT_AFTER_GENESIS=false",
            "PR_IGNORE_CALLSTACK=false",
            f"PR_NETH_RPC_CLIENT_URL={nitro_nethermind_rpc_url}",
            "PR_EXECUTION_MODE=compare",
        ],
        "command": nitro_command,
        "networks": [docker_network_name],
    }


def get_docker_compose_config(
    chain: str,
    nitro_image: str,
    nethermind_image: str,
) -> dict:
    # General config
    docker_network_name = "nethermind-network"
    # Nethermind config
    nethermind_service_name = "nethermind-l2"
    nethermind_rpc_port = 20545
    nethermind_engine_port = 20551
    # Nitro config
    nitro_service_name = "nitro"
    nitro_nethermind_rpc_url = f"http://{nethermind_service_name}:{nethermind_rpc_port}"
    # Return config
    return {
        "services": {
            nethermind_service_name: get_nethermind_config(
                chain=chain,
                nethermind_service_name=nethermind_service_name,
                nethermind_image=nethermind_image,
                nethermind_rpc_port=nethermind_rpc_port,
                nethermind_engine_port=nethermind_engine_port,
                docker_network_name=docker_network_name,
            ),
            nitro_service_name: get_nitro_config(
                chain=chain,
                nitro_service_name=nitro_service_name,
                nitro_image=nitro_image,
                nitro_nethermind_rpc_url=nitro_nethermind_rpc_url,
                docker_network_name=docker_network_name,
            ),
        },
        "networks": {
            docker_network_name: {
                "name": docker_network_name,
            },
        },
    }


def generate_custom_node_data(
    docker_registry: str,
    docker_registry_username: str,
    docker_registry_password: str,
    gh_username: str,
    base_tag: str,
    chain: str,
    nitro_image: str,
    nethermind_image: str,
    setup_script_template_file: Path,
    allowed_ips: str = "",
    ssh_keys: str = "",
    tags: str = "",
    timeout: int = DEFAULT_TIMEOUT,
) -> dict[str, str]:
    setup_script_file = Template(
        setup_script_template_file.read_text(),
        extensions=[
            AnsibleCoreFiltersExtension,
        ],
    )

    data = {
        "docker_registry": {
            "url": docker_registry,
            "username": docker_registry_username,
            "password": docker_registry_password,
        },
        "docker_compose_file": get_docker_compose_config(
            chain=chain,
            nitro_image=nitro_image,
            nethermind_image=nethermind_image,
        ),
    }

    setup_script = setup_script_file.render(**data)
    setup_script_gzip = gzip.compress(setup_script.encode("utf-8"))
    setup_script_b64 = base64.b64encode(setup_script_gzip).decode("utf-8")

    return {
        "base_tag": base_tag,
        "github_username": gh_username,
        "custom_node_data": CUSTOM_NODE_NAME,
        "custom_machine_type": CUSTOM_MACHINE_TYPE_PER_CHAIN.get(
            chain,
            DEFAULT_CUSTOM_MACHINE_TYPE,
        ),
        "setup_script": setup_script_b64,
        "tags": tags,
        "allowed_ips": allowed_ips,
        "ssh_keys": ssh_keys,
        "timeout": str(timeout),
    }


if __name__ == "__main__":
    # Get inputs
    ## Docker registry
    docker_registry = os.environ.get("DOCKER_REGISTRY")
    if not docker_registry:
        raise ValueError("DOCKER_REGISTRY is not set")
    docker_registry_username = os.environ.get("DOCKER_REGISTRY_USERNAME")
    if not docker_registry_username:
        raise ValueError("DOCKER_REGISTRY_USERNAME is not set")
    docker_registry_password = os.environ.get("DOCKER_REGISTRY_PASSWORD")
    if not docker_registry_password:
        raise ValueError("DOCKER_REGISTRY_PASSWORD is not set")
    ## GitHub workflow
    gh_username = os.environ.get("GH_USERNAME")
    if not gh_username:
        raise ValueError("GH_USERNAME is not set")
    base_tag = os.environ.get("BASE_TAG")
    if not base_tag:
        raise ValueError("BASE_TAG is not set")
    try:
        timeout = int(os.environ.get("TIMEOUT", DEFAULT_TIMEOUT))
        timeout = 24 if timeout <= 0 else timeout
    except ValueError:
        raise ValueError("TIMEOUT is not a valid integer")
    ## Setup script
    chain = os.environ.get("CHAIN")
    if not chain:
        raise ValueError("CHAIN is not set")
    nitro_image = os.environ.get("NITRO_IMAGE")
    if not nitro_image:
        raise ValueError("NITRO_IMAGE is not set")
    nethermind_image = os.environ.get("NETHERMIND_IMAGE")
    if not nethermind_image:
        raise ValueError("NETHERMIND_IMAGE is not set")
    try:
        setup_script_template_file = Path(os.environ.get("SETUP_SCRIPT_TEMPLATE"))
    except ValueError:
        raise ValueError("SETUP_SCRIPT_TEMPLATE is not a valid path")
    if not setup_script_template_file.exists():
        raise ValueError("SETUP_SCRIPT_TEMPLATE does not exist")
    tags = os.environ.get("TAGS", "")
    allowed_ips = os.environ.get("ALLOWED_IPS", "")
    ssh_keys = os.environ.get("SSH_KEYS", "")

    # Generate custom node data
    custom_node_data = generate_custom_node_data(
        docker_registry=docker_registry,
        docker_registry_username=docker_registry_username,
        docker_registry_password=docker_registry_password,
        gh_username=gh_username,
        base_tag=base_tag,
        chain=chain,
        nitro_image=nitro_image,
        nethermind_image=nethermind_image,
        setup_script_template_file=setup_script_template_file,
        allowed_ips=allowed_ips,
        ssh_keys=ssh_keys,
        tags=tags,
        timeout=timeout,
    )

    with CUSTOM_NODE_DATA_FILE.open("w") as f:
        json.dump(custom_node_data, f)
