namespace Lyra.Core.Blocks
{
    public class NodeInfo
    {
        public string PublicKey { get; set; }
        public string IPAddress { get; set; }

        public override string ToString()
        {
            return PublicKey + IPAddress;
        }
    }

   
}