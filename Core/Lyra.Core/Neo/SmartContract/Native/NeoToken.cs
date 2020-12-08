using Neo.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Neo.SmartContract.Native
{
    public class NeoToken
    {
        public UInt160 GetCommitteeAddress(StoreView snapshot)
        {
            throw new NotImplementedException();
        }
    }

    public class GasToken
    {
        internal protected virtual void Mint(ApplicationEngine engine, UInt160 account, BigInteger amount, bool callOnPayment)
        {
            
        }

        public BigInteger Factor { get; }

        [ContractMethod(0_01000000, CallFlags.ReadStates)]
        public virtual BigInteger BalanceOf(StoreView snapshot, UInt160 account)
        {
            throw new NotImplementedException();
            //StorageItem storage = snapshot.Storages.TryGet(CreateStorageKey(Prefix_Account).Add(account));
            //if (storage is null) return BigInteger.Zero;
            //return storage.GetInteroperable<TState>().Balance;
        }
    }
}
