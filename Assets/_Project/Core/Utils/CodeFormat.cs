namespace _Project.Core.Utils
{
    public static class CodeFormat
    {
        public static string EnumFormat = 
        @"
namespace {0}
{{
    public enum {1}
    {{
        {2}
    }}
}}
";

    }
}