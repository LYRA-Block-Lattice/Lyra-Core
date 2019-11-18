using System;
namespace Lyra.Node2
{
    public static class NodeGlobalParameters
    {
        public static bool IsSingleNodeTestnet { get; set; }

        //public static bool IsTestnet { get; set; }

       public static string Network_Id { get; set; }

        public const string DEFAULT_DATABASE_NAME = "lyra";
        
    }
}
