//using Lyra.Core.Blocks;
//using Lyra.Core.Utils;
//using Microsoft.Extensions.Logging;
//using System;
//using System.Collections.Concurrent;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace Lyra.Core.Decentralize
//{
//    public class Orphanage
//    {
//        ILogger _log;
//        private Func<AuthState, Task> OnAuthStateReady;
//        private Func<AuthorizingMsg, Task> OnAuthorizingMsgReady;
//        private Func<List<AuthorizedMsg>, Task> OnAuthorizedMsgReady;
//        private Func<List<AuthorizerCommitMsg>, Task> OnAuthorizerCommitMsgReady;

//        ConcurrentDictionary<string, AuthState> _orphanAuthStates { get; } = new ConcurrentDictionary<string, AuthState>();
//        ConcurrentDictionary<string, AuthorizingMsg> _orphanAuthorizingMsg { get; } = new ConcurrentDictionary<string, AuthorizingMsg>();
//        ConcurrentDictionary<string, List<AuthorizedMsg>> _orphanAuthorizedMsg { get; } = new ConcurrentDictionary<string, List<AuthorizedMsg>>();
//        ConcurrentDictionary<string, List<AuthorizerCommitMsg>> _orphanAuthorizerCommitMsg { get; } = new ConcurrentDictionary<string, List<AuthorizerCommitMsg>>();

//        private DagSystem _sys;
//        public Orphanage(DagSystem sys, Func<AuthState, Task> onAuthStateReady,
//                        Func<AuthorizingMsg, Task> onAuthorizingMsgReady,
//                        Func<List<AuthorizedMsg>, Task> onAuthorizedMsgReady,
//                        Func<List<AuthorizerCommitMsg>, Task> onAuthorizerCommitMsgReady)
//        {
//            _log = new SimpleLogger("Orphanage").Logger;
//            _sys = sys;

//            OnAuthStateReady = onAuthStateReady;
//            OnAuthorizingMsgReady = onAuthorizingMsgReady;
//            OnAuthorizedMsgReady = onAuthorizedMsgReady;
//            OnAuthorizerCommitMsgReady = onAuthorizerCommitMsgReady;
//        }

//        public async Task<bool> TryAddOneAsync<T>(T orphan)
//        {
//            switch(orphan)
//            {
//                case AuthState authState:
//                    if (await IsThisBlockOrphanAsync(authState.InputMsg.Block))
//                        return _orphanAuthStates.TryAdd(authState.InputMsg.Block.Hash, authState);
//                    return false;                    
//                case AuthorizingMsg msg1:
//                    if (await IsThisBlockOrphanAsync(msg1.Block))                      
//                        return _orphanAuthorizingMsg.TryAdd(msg1.Hash, msg1);
//                    return false;
//                //case AuthorizedMsg msg2:
//                //    if (await IsThisPrevHashExists(msg2.BlockHash))
//                //        return false;
//                //    if (_orphanAuthorizedMsg.ContainsKey(msg2.Hash))
//                //    {
//                //        var list = _orphanAuthorizedMsg[msg2.Hash];
//                //        list.Add(msg2);
//                //        return true;
//                //    }
//                //    else
//                //    {
//                //        var list = new List<AuthorizedMsg>();
//                //        list.Add(msg2);
//                //        return _orphanAuthorizedMsg.TryAdd(msg2.Hash, list);
//                //    }
//                //case AuthorizerCommitMsg msg3:
//                //    if (await IsThisPrevHashExists(msg3.BlockHash))
//                //        return false;
//                //    if (_orphanAuthorizerCommitMsg.ContainsKey(msg3.Hash))
//                //    {
//                //        var list = _orphanAuthorizerCommitMsg[msg3.Hash];
//                //        list.Add(msg3);
//                //        return true;
//                //    }
//                //    else
//                //    {
//                //        var list = new List<AuthorizerCommitMsg>();
//                //        list.Add(msg3);
//                //        return _orphanAuthorizerCommitMsg.TryAdd(msg3.Hash, list);
//                //    }
//            }
//            return false;
//        }

//        /// <summary>
//        /// 
//        /// </summary>
//        /// <param name="sender"></param>
//        /// <param name="hash">Block Hash</param>
//        public async Task BlockAddedAsync(string hash)
//        {
//            await PickupLuckyOneAsync(hash, _orphanAuthStates, OnAuthStateReady);
//            await PickupLuckyOneAsync(hash, _orphanAuthorizingMsg, OnAuthorizingMsgReady);
//            //await PickupLuckyOneAsync(hash, _orphanAuthorizedMsg, OnAuthorizedMsgReady);
//            //await PickupLuckyOneAsync(hash, _orphanAuthorizerCommitMsg, OnAuthorizerCommitMsgReady);
//        }

//        private async Task PickupLuckyOneAsync<T>(string hash, ConcurrentDictionary<string, T> orphans, Func<T, Task> handler)
//        {
//            if(orphans.ContainsKey(hash))
//            {
//                T luckone;
//                if (orphans.TryRemove(hash, out luckone))
//                {
//                    _log.LogInformation("Released an orphan!");
//                    await handler(luckone);
//                }                    
//            }
//        }

//        public async Task<bool> IsThisBlockOrphanAsync(Block blockx)
//        {
//            var block = blockx as TransactionBlock;
//            if (block == null)
//                return false;

//            // double spent dection
//            if (_orphanAuthStates.Values
//                .Where(s => s.InputMsg.Block is TransactionBlock)
//                .Select(t => t.InputMsg.Block as TransactionBlock)
//                .Any(a => a.AccountID == block.AccountID && a.Height == block.Height))
//                return false;

//            if (_orphanAuthorizingMsg.Values
//                .Where(s => s.Block is TransactionBlock)
//                .Select(t => t.Block as TransactionBlock)
//                .Any(a => a.AccountID == block.AccountID && a.Height == block.Height))
//                return false;

//            if (block.PreviousHash != null)
//            {
//                var prevBlock = await _sys.Storage.FindBlockByHashAsync(block.PreviousHash);
//                if(prevBlock == null)
//                {
//                    _log.LogInformation("Found an orphan!");
//                }
//                return prevBlock == null;
//            }

//            return false;
//        }
//    }
//}
