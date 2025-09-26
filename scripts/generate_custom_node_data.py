import os
import json
import gzip
import base64

from jinja2 import Template


# Constants
CUSTOM_NODE_DATA_FILE = "custom_node_data.json"
CUSTOM_NODE_NAME = "nethermind-arb"
CUSTOM_MACHINE_TYPE_PER_CHAIN = {"sepolia": "g6-linode-8"}


def generate_custom_node_data(
    gh_username: str,
    base_tag: str,
    chain: str,
    nitro_image: str,
    nethermind_image: str,
    setup_script_template_file: str,
    allowed_ips: str = "",
    ssh_keys: str = "",
    tags: str = "",
    timeout: int = 24,
) -> dict[str, str]:
    with open(setup_script_template_file, "r") as f:
        setup_script_file = Template(f.read())

    data = {
        "nitro_image": nitro_image,
        "nethermind_image": nethermind_image,
        "chain": chain,
    }

    setup_script = setup_script_file.render(**data)
    setup_script_gzip = gzip.compress(setup_script.encode("utf-8"))
    setup_script_b64 = base64.b64encode(setup_script_gzip).decode("utf-8")

    return {
        "base_tag": base_tag,
        "github_username": gh_username,
        "custom_node_data": CUSTOM_NODE_NAME,
        "custom_machine_type": CUSTOM_MACHINE_TYPE_PER_CHAIN[chain],
        "setup_script": setup_script_b64,
        "tags": ",".join(tags),
        "allowed_ips": allowed_ips,
        "ssh_keys": ssh_keys,
        "timeout": str(timeout),
    }


if __name__ == "__main__":
    # Get inputs
    gh_username = os.environ.get("GH_USERNAME")
    if not gh_username:
        raise ValueError("GH_USERNAME is not set")
    base_tag = os.environ.get("BASE_TAG")
    if not base_tag:
        raise ValueError("BASE_TAG is not set")
    chain = os.environ.get("CHAIN")
    if not chain:
        raise ValueError("CHAIN is not set")
    nitro_image = os.environ.get("NITRO_IMAGE")
    if not nitro_image:
        raise ValueError("NITRO_IMAGE is not set")
    nethermind_image = os.environ.get("NETHERMIND_IMAGE")
    if not nethermind_image:
        raise ValueError("NETHERMIND_IMAGE is not set")
    setup_script_template_file = os.environ.get("SETUP_SCRIPT_TEMPLATE")
    if not setup_script_template_file:
        raise ValueError("SETUP_SCRIPT_TEMPLATE is not set")

    # Generate custom node data
    custom_node_data = generate_custom_node_data(
        gh_username=gh_username,
        base_tag=base_tag,
        chain=chain,
        nitro_image=nitro_image,
        nethermind_image=nethermind_image,
        setup_script_template_file=setup_script_template_file,
    )

    with open(CUSTOM_NODE_DATA_FILE, "w") as f:
        json.dump(custom_node_data, f)
