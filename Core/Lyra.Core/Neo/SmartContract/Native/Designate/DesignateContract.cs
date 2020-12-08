#pragma warning disable IDE0051

using Neo.Cryptography;
using Neo.Cryptography.ECC;
using Neo.IO;
using Neo.IO.Caching;
using Neo.Ledger;
using Neo.Persistence;
using Neo.VM;
using Neo.VM.Types;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Neo.SmartContract.Native.Designate
{
    public sealed class DesignateContract : NativeContract
    {
        public override string Name => "Designation";
        public override int Id => -5;

        internal DesignateContract()
        {
        }

        [ContractMethod(0_01000000, CallFlags.ReadStates)]
        public ECPoint[] GetDesignatedByRole(StoreView snapshot, Role role, uint index)
        {
            if (!Enum.IsDefined(typeof(Role), role))
                throw new ArgumentOutOfRangeException(nameof(role));
            if (snapshot.Height + 1 < index)
                throw new ArgumentOutOfRangeException(nameof(index));
            byte[] key = CreateStorageKey((byte)role).AddBigEndian(index).ToArray();
            byte[] boundary = CreateStorageKey((byte)role).ToArray();
            return snapshot.Storages.FindRange(key, boundary, SeekDirection.Backward)
                .Select(u => u.Value.GetInteroperable<NodeList>().ToArray())
                .FirstOrDefault() ?? System.Array.Empty<ECPoint>();
        }

        [ContractMethod(0, CallFlags.WriteStates)]
        private void DesignateAsRole(ApplicationEngine engine, Role role, ECPoint[] nodes)
        {
            if (nodes.Length == 0 || nodes.Length > 32)
                throw new ArgumentException();
            if (!Enum.IsDefined(typeof(Role), role))
                throw new ArgumentOutOfRangeException(nameof(role));
            if (!CheckCommittee(engine))
                throw new InvalidOperationException(nameof(DesignateAsRole));
            if (engine.Snapshot.PersistingBlock is null)
                throw new InvalidOperationException(nameof(DesignateAsRole));
            uint index = engine.Snapshot.PersistingBlock.Index + 1;
            var key = CreateStorageKey((byte)role).AddBigEndian(index);
            if (engine.Snapshot.Storages.Contains(key))
                throw new InvalidOperationException();
            NodeList list = new NodeList();
            list.AddRange(nodes);
            list.Sort();
            engine.Snapshot.Storages.Add(key, new StorageItem(list));
        }

        private class NodeList : List<ECPoint>, IInteroperable
        {
            public void FromStackItem(StackItem stackItem)
            {
                foreach (StackItem item in (VM.Types.Array)stackItem)
                    Add(item.GetSpan().AsSerializable<ECPoint>());
            }

            public StackItem ToStackItem(ReferenceCounter referenceCounter)
            {
                return new VM.Types.Array(referenceCounter, this.Select(p => (StackItem)p.ToArray()));
            }
        }
    }
}
