using Neo.Cryptography.ECC;
using Neo.IO;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract.Native;
using Neo.SmartContract.Native.Oracle;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Array = Neo.VM.Types.Array;

namespace Neo.SmartContract
{
    partial class ApplicationEngine
    {
        public const int MaxEventName = 32;
        public const int MaxNotificationSize = 1024;

        public static readonly InteropDescriptor System_Runtime_Platform = Register("System.Runtime.Platform", nameof(GetPlatform), 0_00000250, CallFlags.None, true);
        public static readonly InteropDescriptor System_Runtime_GetTrigger = Register("System.Runtime.GetTrigger", nameof(Trigger), 0_00000250, CallFlags.None, true);
        public static readonly InteropDescriptor System_Runtime_GetTime = Register("System.Runtime.GetTime", nameof(GetTime), 0_00000250, CallFlags.None, true);
        public static readonly InteropDescriptor System_Runtime_GetScriptContainer = Register("System.Runtime.GetScriptContainer", nameof(GetScriptContainer), 0_00000250, CallFlags.None, true);
        public static readonly InteropDescriptor System_Runtime_GetExecutingScriptHash = Register("System.Runtime.GetExecutingScriptHash", nameof(CurrentScriptHash), 0_00000400, CallFlags.None, true);
        public static readonly InteropDescriptor System_Runtime_GetCallingScriptHash = Register("System.Runtime.GetCallingScriptHash", nameof(CallingScriptHash), 0_00000400, CallFlags.None, true);
        public static readonly InteropDescriptor System_Runtime_GetEntryScriptHash = Register("System.Runtime.GetEntryScriptHash", nameof(EntryScriptHash), 0_00000400, CallFlags.None, true);
        public static readonly InteropDescriptor System_Runtime_CheckWitness = Register("System.Runtime.CheckWitness", nameof(CheckWitness), 0_00030000, CallFlags.None, true);
        public static readonly InteropDescriptor System_Runtime_GetInvocationCounter = Register("System.Runtime.GetInvocationCounter", nameof(GetInvocationCounter), 0_00000400, CallFlags.None, true);
        public static readonly InteropDescriptor System_Runtime_Log = Register("System.Runtime.Log", nameof(RuntimeLog), 0_01000000, CallFlags.AllowNotify, false);
        public static readonly InteropDescriptor System_Runtime_Notify = Register("System.Runtime.Notify", nameof(RuntimeNotify), 0_01000000, CallFlags.AllowNotify, false);
        public static readonly InteropDescriptor System_Runtime_GetNotifications = Register("System.Runtime.GetNotifications", nameof(GetNotifications), 0_00010000, CallFlags.None, true);
        public static readonly InteropDescriptor System_Runtime_GasLeft = Register("System.Runtime.GasLeft", nameof(GasLeft), 0_00000400, CallFlags.None, true);

        protected internal string GetPlatform()
        {
            return "NEO";
        }

        protected internal ulong GetTime()
        {
            return Snapshot.PersistingBlock.Timestamp;
        }

        protected internal IInteroperable GetScriptContainer()
        {
            return ScriptContainer as IInteroperable;
        }

        protected internal bool CheckWitness(byte[] hashOrPubkey)
        {
            UInt160 hash = hashOrPubkey.Length switch
            {
                20 => new UInt160(hashOrPubkey),
                33 => Contract.CreateSignatureRedeemScript(ECPoint.DecodePoint(hashOrPubkey, ECCurve.Secp256r1)).ToScriptHash(),
                _ => throw new ArgumentException()
            };
            return CheckWitnessInternal(hash);
        }

        protected internal bool CheckWitnessInternal(UInt160 hash)
        {
            if (hash.Equals(CallingScriptHash)) return true;

            if (ScriptContainer is Transaction tx)
            {
                Signer[] signers;
                OracleResponse response = tx.GetAttribute<OracleResponse>();
                if (response is null)
                {
                    signers = tx.Signers;
                }
                else
                {
                    OracleRequest request = NativeContract.Oracle.GetRequest(Snapshot, response.Id);
                    signers = Snapshot.GetTransaction(request.OriginalTxid).Signers;
                }
                Signer signer = signers.FirstOrDefault(p => p.Account.Equals(hash));
                if (signer is null) return false;
                if (signer.Scopes == WitnessScope.Global) return true;
                if (signer.Scopes.HasFlag(WitnessScope.CalledByEntry))
                {
                    if (CallingScriptHash == null || CallingScriptHash == EntryScriptHash)
                        return true;
                }
                if (signer.Scopes.HasFlag(WitnessScope.CustomContracts))
                {
                    if (signer.AllowedContracts.Contains(CurrentScriptHash))
                        return true;
                }
                if (signer.Scopes.HasFlag(WitnessScope.CustomGroups))
                {
                    // Check allow state callflag

                    if (!CurrentContext.GetState<ExecutionContextState>().CallFlags.HasFlag(CallFlags.ReadStates))
                        throw new InvalidOperationException($"Cannot call this SYSCALL without the flag AllowStates.");

                    var contract = Snapshot.Contracts[CallingScriptHash];
                    // check if current group is the required one
                    if (contract.Manifest.Groups.Select(p => p.PubKey).Intersect(signer.AllowedGroups).Any())
                        return true;
                }
                return false;
            }

            // Check allow state callflag

            if (!CurrentContext.GetState<ExecutionContextState>().CallFlags.HasFlag(CallFlags.ReadStates))
                throw new InvalidOperationException($"Cannot call this SYSCALL without the flag AllowStates.");

            // only for non-Transaction types (Block, etc)

            var hashes_for_verifying = ScriptContainer.GetScriptHashesForVerifying(Snapshot);
            return hashes_for_verifying.Contains(hash);
        }

        protected internal int GetInvocationCounter()
        {
            if (!invocationCounter.TryGetValue(CurrentScriptHash, out var counter))
            {
                invocationCounter[CurrentScriptHash] = counter = 1;
            }
            return counter;
        }

        protected internal void RuntimeLog(byte[] state)
        {
            if (state.Length > MaxNotificationSize) throw new ArgumentException();
            string message = Utility.StrictUTF8.GetString(state);
            Log?.Invoke(this, new LogEventArgs(ScriptContainer, CurrentScriptHash, message));
        }

        protected internal void RuntimeNotify(byte[] eventName, Array state)
        {
            if (eventName.Length > MaxEventName) throw new ArgumentException();
            using (MemoryStream ms = new MemoryStream(MaxNotificationSize))
            using (BinaryWriter writer = new BinaryWriter(ms))
            {
                BinarySerializer.Serialize(writer, state, MaxNotificationSize);
            }
            SendNotification(CurrentScriptHash, Utility.StrictUTF8.GetString(eventName), state);
        }

        protected internal void SendNotification(UInt160 hash, string eventName, Array state)
        {
            NotifyEventArgs notification = new NotifyEventArgs(ScriptContainer, hash, eventName, (Array)state.DeepCopy());
            Notify?.Invoke(this, notification);
            notifications ??= new List<NotifyEventArgs>();
            notifications.Add(notification);
        }

        protected internal NotifyEventArgs[] GetNotifications(UInt160 hash)
        {
            IEnumerable<NotifyEventArgs> notifications = Notifications;
            if (hash != null) // must filter by scriptHash
                notifications = notifications.Where(p => p.ScriptHash == hash);
            NotifyEventArgs[] array = notifications.ToArray();
            if (array.Length > Limits.MaxStackSize) throw new InvalidOperationException();
            return array;
        }
    }
}
