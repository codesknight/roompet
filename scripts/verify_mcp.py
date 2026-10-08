"""End-to-end check: launch the MCP for Unity server over stdio (the exact way DSH
does) and drive the running Unity Editor through it.

Usage:
    D:\\projects\\dsh-unity\\.venv\\Scripts\\python.exe scripts\\verify_mcp.py

Environment honoured:
    UNITY_MCP_STATUS_DIR  where the Editor wrote its bridge status file
"""

import asyncio
import json
import os
import sys

from mcp import ClientSession, StdioServerParameters
from mcp.client.stdio import stdio_client

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SERVER_EXE = os.path.join(ROOT, ".venv", "Scripts", "mcp-for-unity.exe")
STATUS_DIR = os.environ.get("UNITY_MCP_STATUS_DIR", os.path.join(ROOT, ".unity-mcp-status"))


def show(label, result):
    parts = []
    for block in result.content:
        text = getattr(block, "text", None)
        if text is not None:
            parts.append(text)
    body = "\n".join(parts)
    print(f"\n--- {label} (isError={result.isError}) ---")
    print(body[:1200] if len(body) > 1200 else body)
    return body


async def main() -> int:
    params = StdioServerParameters(
        command=SERVER_EXE,
        args=[],
        env={**os.environ, "UNITY_MCP_STATUS_DIR": STATUS_DIR},
    )

    async with stdio_client(params) as (read, write):
        async with ClientSession(read, write) as session:
            await session.initialize()
            info = getattr(session, "server_info", None) or getattr(
                session, "initialize_result", None)
            print(f"server initialized: {info}")

            tools = await session.list_tools()
            names = sorted(t.name for t in tools.tools)
            print(f"\ntools exposed: {len(names)}")
            print("  " + ", ".join(names))

            show("manage_scene get_hierarchy (before)", await session.call_tool(
                "manage_scene", {"action": "get_hierarchy"}))

            show("manage_gameobject create Cube", await session.call_tool(
                "manage_gameobject", {
                    "action": "create",
                    "name": "DshVerifyCube",
                    "primitive_type": "Cube",
                    "position": [0, 0, 0],
                }))

            after = show("manage_scene get_hierarchy (after)", await session.call_tool(
                "manage_scene", {"action": "get_hierarchy"}))
            assert "DshVerifyCube" in after, "created object not visible in hierarchy"

            show("read_console get", await session.call_tool(
                "read_console", {"action": "get", "count": 5}))

    print("\nEND-TO-END OK")
    return 0


if __name__ == "__main__":
    sys.exit(asyncio.run(main()))
