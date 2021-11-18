namespace DexServer.Ext
{
    public class DexResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
    }
    public class DexAddress : DexResult
    {
        public string Owner { get; set; }
        public string Address { get; set; }
        public string Blockchain { get; set; }
        public string Provider { get; set; }
        public string Network { get; set; }
        public string Contract { get; set; }
    }
}
