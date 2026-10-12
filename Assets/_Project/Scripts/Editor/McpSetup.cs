using UnityEditor;

namespace IdleArcade.EditorTools
{
    /// <summary>
    /// Claude Code talks to "MCP for Unity" over stdio (registered with `claude mcp add ... --transport stdio`),
    /// so the Unity side must run its stdio bridge instead of the default HTTP server.
    /// </summary>
    [InitializeOnLoad]
    static class McpSetup
    {
        const string Key = "MCPForUnity.UseHttpTransport";

        static McpSetup()
        {
            if (EditorPrefs.GetBool(Key, true)) EditorPrefs.SetBool(Key, false);
        }
    }
}
