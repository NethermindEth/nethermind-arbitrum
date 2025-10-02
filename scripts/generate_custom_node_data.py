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
CUSTOM_MACHINE_TYPE_PER_CHAIN = {
    "sepolia": "g6-standard-8",
    "mainnet": "g6-standard-8",
}

DEFAULT_TIMEOUT = 24
DEFAULT_CUSTOM_MACHINE_TYPE = "g6-standard-8"
DEFAULT_BLOCK_PROCESSING_TIMEOUT = 60000


class CustomNodeConfig:
    def __init__(
        self,
        instance_name: str,
        docker_registry: str,
        docker_registry_username: str,
        docker_registry_password: str,
        gh_username: str,
        base_tag: str,
        chain: str,
        chain_rpc_url: str,
        chain_beacon_url: str,
        nitro_image: str,
        nethermind_image: str,
        setup_script_template_file: Path,
        pushgateway_url: str = "",
        allowed_ips: str = "",
        ssh_keys: str = "",
        tags: str = "",
        timeout: int = DEFAULT_TIMEOUT,
        seq_url: str = "",
        seq_api_key: str = "",
    ):
        self.instance_name = instance_name
        self.docker_registry = docker_registry
        self.docker_registry_username = docker_registry_username
        self.docker_registry_password = docker_registry_password
        self.gh_username = gh_username
        self.base_tag = base_tag
        self.chain = chain
        self.chain_rpc_url = chain_rpc_url
        self.chain_beacon_url = chain_beacon_url
        self.nitro_image = nitro_image
        self.nethermind_image = nethermind_image
        self.setup_script_template_file = setup_script_template_file
        self.pushgateway_url = pushgateway_url
        self.allowed_ips = allowed_ips
        self.ssh_keys = ssh_keys
        self.tags = tags
        self.timeout = timeout
        self.seq_url = seq_url
        self.seq_api_key = seq_api_key

    @staticmethod
    def from_env() -> "CustomNodeConfig":
        """Load configuration from environment variables with validation."""
        # Docker registry
        docker_registry = os.environ.get("DOCKER_REGISTRY")
        if not docker_registry:
            raise ValueError("DOCKER_REGISTRY is not set")

        docker_registry_username = os.environ.get("DOCKER_REGISTRY_USERNAME")
        if not docker_registry_username:
            raise ValueError("DOCKER_REGISTRY_USERNAME is not set")

        docker_registry_password = os.environ.get("DOCKER_REGISTRY_PASSWORD")
        if not docker_registry_password:
            raise ValueError("DOCKER_REGISTRY_PASSWORD is not set")

        # GitHub workflow
        gh_username = os.environ.get("GH_USERNAME")
        if not gh_username:
            raise ValueError("GH_USERNAME is not set")

        base_tag = os.environ.get("BASE_TAG")
        if not base_tag:
            raise ValueError("BASE_TAG is not set")

        # Timeout validation
        try:
            timeout = int(os.environ.get("TIMEOUT", DEFAULT_TIMEOUT))
            timeout = DEFAULT_TIMEOUT if timeout <= 0 else timeout
        except ValueError:
            raise ValueError("TIMEOUT is not a valid integer")

        # Setup script
        chain = os.environ.get("CHAIN")
        if not chain:
            raise ValueError("CHAIN is not set")

        chain_rpc_url = os.environ.get("CHAIN_RPC_URL")
        if not chain_rpc_url:
            raise ValueError("CHAIN_RPC_URL is not set")

        chain_beacon_url = os.environ.get("CHAIN_BEACON_URL")
        if not chain_beacon_url:
            raise ValueError("CHAIN_BEACON_URL is not set")

        nitro_image = os.environ.get("NITRO_IMAGE")
        if not nitro_image:
            raise ValueError("NITRO_IMAGE is not set")

        nethermind_image = os.environ.get("NETHERMIND_IMAGE")
        if not nethermind_image:
            raise ValueError("NETHERMIND_IMAGE is not set")

        # Setup script template file validation
        setup_script_template_file = os.environ.get("SETUP_SCRIPT_TEMPLATE")
        if not setup_script_template_file:
            raise ValueError("SETUP_SCRIPT_TEMPLATE is not set")

        try:
            setup_script_template_path = Path(setup_script_template_file)
        except ValueError:
            raise ValueError("SETUP_SCRIPT_TEMPLATE is not a valid path")

        if not setup_script_template_path.exists():
            raise ValueError("SETUP_SCRIPT_TEMPLATE does not exist")

        # Optional environment variables
        pushgateway_url = os.environ.get("PUSHGATEWAY_URL", "")
        tags = os.environ.get("TAGS", "")
        allowed_ips = os.environ.get("ALLOWED_IPS", "")
        ssh_keys = os.environ.get("SSH_KEYS", "")
        seq_url = os.environ.get("SEQ_URL", "")
        seq_api_key = os.environ.get("SEQ_API_KEY", "")

        # Instance name for metrics identification
        instance_name = f"{base_tag}-{gh_username}-{chain}"

        return CustomNodeConfig(
            instance_name=instance_name,
            docker_registry=docker_registry,
            docker_registry_username=docker_registry_username,
            docker_registry_password=docker_registry_password,
            gh_username=gh_username,
            base_tag=base_tag,
            chain=chain,
            chain_rpc_url=chain_rpc_url,
            chain_beacon_url=chain_beacon_url,
            nitro_image=nitro_image,
            nethermind_image=nethermind_image,
            setup_script_template_file=setup_script_template_path,
            pushgateway_url=pushgateway_url,
            allowed_ips=allowed_ips,
            ssh_keys=ssh_keys,
            tags=tags,
            timeout=timeout,
            seq_url=seq_url,
            seq_api_key=seq_api_key,
        )


def get_nethermind_config(
    instance_name: str,
    chain: str,
    nethermind_service_name: str,
    nethermind_image: str,
    nethermind_rpc_port: int,
    nethermind_engine_port: int,
    nethermind_p2p_port: int,
    nethermind_metrics_port: int,
    docker_network_name: str,
    block_processing_timeout: int = DEFAULT_BLOCK_PROCESSING_TIMEOUT,
    pushgateway_url: str = "",
    seq_url: str = "",
    seq_api_key: str = "",
    # TODO: Add more flags options as needed
) -> dict:
    # Paths
    nethermind_data_dir = "/app/nethermind_db"
    nethermind_host_data_dir = "./nethermind-data"

    nethermind_command = []
    if chain == "sepolia":
        nethermind_command += [
            "-c=arbitrum-sepolia-archive",
        ]
    elif chain == "mainnet":
        nethermind_command += [
            "-c=arbitrum-mainnet-archive",
        ]

    # Add default flags
    nethermind_command += [
        f"--data-dir={nethermind_data_dir}",
        "--stylustarget-amd64=x86_64-linux-unknown",
        "--JsonRpc.Host=0.0.0.0",
        "--JsonRpc.EngineHost=0.0.0.0",
        f"--Arbitrum.BlockProcessingTimeout={block_processing_timeout}",
        "--log=debug",
    ]
    nethermind_command += [
        "--Init.DiscoveryEnabled=false",
        "--Network.MaxActivePeers=0",
        "--Sync.SnapSync=false",
        "--Sync.FastSync=false",
    ]
    ## Add metrics flags
    nethermind_command += [
        "--Metrics.Enabled=true",
        f"--Metrics.ExposePort={nethermind_metrics_port}",
        "--Metrics.ExposeHost=0.0.0.0",
        f"--Metrics.NodeName={instance_name}",
    ]
    if pushgateway_url:
        nethermind_command += [
            f"--Metrics.PushGatewayUrl={pushgateway_url}",
        ]

    if seq_url and seq_api_key:
        nethermind_command += [
            "--Seq.MinLevel=Info",
            f"--Seq.ServerUrl={seq_url}",
            f"--Seq.ApiKey={seq_api_key}",
        ]

    # Return config
    return {
        "image": nethermind_image,
        "container_name": nethermind_service_name,
        "restart": "no",
        "ports": [
            f"{nethermind_rpc_port}:{nethermind_rpc_port}",
            f"{nethermind_engine_port}:{nethermind_engine_port}",
            f"{nethermind_p2p_port}:{nethermind_p2p_port}/tcp",
            f"{nethermind_p2p_port}:{nethermind_p2p_port}/udp",
            f"{nethermind_metrics_port}:{nethermind_metrics_port}",
        ],
        "volumes": [f"{nethermind_host_data_dir}:{nethermind_data_dir}"],
        "environment": [
            "STYLUS_ARCH=amd64",
            "STYLUS_TARGET=x86_64-linux-unknown",
        ],
        "command": nethermind_command,
        "networks": [docker_network_name],
        "healthcheck": {
            "test": [
                "CMD-SHELL",
                f"timeout 5 bash -c '</dev/tcp/localhost/{nethermind_rpc_port}' || exit 1",
            ],
            "interval": "10s",
            "timeout": "5s",
            "retries": 10,
            "start_period": "30s",
        },
    }


def get_nitro_config(
    chain: str,
    chain_rpc_url: str,
    chain_beacon_url: str,
    nitro_service_name: str,
    nethermind_service_name: str,
    nitro_image: str,
    nitro_nethermind_rpc_url: str,
    docker_network_name: str,
) -> dict:
    # Paths
    nitro_data_dir = "/tmp/nitro-data"
    nitro_host_data_dir = "./nitro-data"

    # Command
    nitro_command = []
    if chain == "sepolia":
        nitro_command += [
            "--chain.id=421614",
        ]
    elif chain == "mainnet":
        nitro_command += [
            "--chain.id=42161",
        ]

    nitro_command += [
        f"--parent-chain.connection.url={chain_rpc_url}",
        f"--parent-chain.blob-client.beacon-url={chain_beacon_url}",
        "--persistent.global-config=/tmp/nitro-data",
        "--execution.forwarding-target=null",
        "--execution.enable-prefetch-block=false",
    ]
    return {
        "image": nitro_image,
        "container_name": nitro_service_name,
        "depends_on": {
            nethermind_service_name: {
                "condition": "service_healthy",
            },
        },
        "restart": "no",
        "ports": [],
        "volumes": [
            f"{nitro_host_data_dir}:{nitro_data_dir}",
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


def get_docker_compose_config(config: CustomNodeConfig) -> dict:
    # General config
    docker_network_name = "nethermind-network"
    # Nethermind config
    nethermind_service_name = "nethermind-l2"
    nethermind_rpc_port = 20545
    nethermind_engine_port = 20551
    nethermind_p2p_port = 30303
    nethermind_metrics_port = 8008
    # Nitro config
    nitro_service_name = "nitro"
    nitro_nethermind_rpc_url = f"http://{nethermind_service_name}:{nethermind_rpc_port}"
    # Return config
    return {
        "services": {
            nethermind_service_name: get_nethermind_config(
                instance_name=config.instance_name,
                chain=config.chain,
                nethermind_service_name=nethermind_service_name,
                nethermind_image=config.nethermind_image,
                nethermind_rpc_port=nethermind_rpc_port,
                nethermind_engine_port=nethermind_engine_port,
                nethermind_p2p_port=nethermind_p2p_port,
                nethermind_metrics_port=nethermind_metrics_port,
                docker_network_name=docker_network_name,
                pushgateway_url=config.pushgateway_url,
                seq_url=config.seq_url,
                seq_api_key=config.seq_api_key,
            ),
            nitro_service_name: get_nitro_config(
                chain=config.chain,
                chain_rpc_url=config.chain_rpc_url,
                chain_beacon_url=config.chain_beacon_url,
                nitro_service_name=nitro_service_name,
                nethermind_service_name=nethermind_service_name,
                nitro_image=config.nitro_image,
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


def generate_custom_node_data(config: CustomNodeConfig) -> dict[str, str]:
    setup_script_file = Template(
        config.setup_script_template_file.read_text(),
        extensions=[
            AnsibleCoreFiltersExtension,
        ],
    )

    data = {
        "docker_registry": {
            "url": config.docker_registry,
            "username": config.docker_registry_username,
            "password": config.docker_registry_password,
        },
        "docker_compose_file": get_docker_compose_config(config),
    }

    setup_script = setup_script_file.render(**data)
    setup_script_gzip = gzip.compress(setup_script.encode("utf-8"))
    setup_script_b64 = base64.b64encode(setup_script_gzip).decode("utf-8")

    return {
        "base_tag": config.base_tag,
        "github_username": config.gh_username,
        "custom_node_name": CUSTOM_NODE_NAME,
        "custom_node_type": CUSTOM_MACHINE_TYPE_PER_CHAIN.get(
            config.chain,
            DEFAULT_CUSTOM_MACHINE_TYPE,
        ),
        "setup_script": setup_script_b64,
        "tags": config.tags,
        "allowed_ips": config.allowed_ips,
        "ssh_keys": config.ssh_keys,
        "timeout": str(config.timeout),
    }


if __name__ == "__main__":
    # Load configuration from environment variables
    config = CustomNodeConfig.from_env()

    # Generate custom node data
    custom_node_data = generate_custom_node_data(config)

    with CUSTOM_NODE_DATA_FILE.open("w") as f:
        json.dump(custom_node_data, f)
