
using Lyra;
using Neo.Persistence;

namespace Neo.Network.P2P.Payloads
{
    public interface IInventory : IVerifiable
    {
        UInt256 Hash { get; }

        InventoryType InventoryType { get; }

        bool Verify(StoreView snapshot);
    }
}
