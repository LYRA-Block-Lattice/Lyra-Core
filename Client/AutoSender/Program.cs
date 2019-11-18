using LyraLexWeb.Models;
using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.IO;
using System.Threading.Tasks;

namespace AutoSender
{
    class Program
    {
        static void Main(string[] args)
        {
            if(args.Length == 0)
            {
                Console.WriteLine("AutoSender walletPath walletName netName webapiUrl mongodbUrl");
                return;
            }

            DoWork(args[0], args[1], args[2], args[3], args[4]).Wait();
        }

        private static async Task<int> DoWork(string walletPath, string walletName, string netName, string webapiUrl, string mongodbUrl)
        {
            // open wallet
            WalletUitl wc = new WalletUitl();
            await wc.OpenWalletFile(netName, walletPath, walletName);

            // open mongodb
            var client = new MongoClient(mongodbUrl);
            var db = client.GetDatabase("LexWeb");
            var lexReqs = db.GetCollection<FreeLeXRequest>("lexreq");
            var filter = Builders<FreeLeXRequest>.Filter.Eq("State", 0);
            var updateDef = Builders<FreeLeXRequest>.Update.Set(o => o.SentTime, DateTime.Now);
                                
            while (true)
            {
                try
                {
                    var findResult = await lexReqs.FindAsync(filter);
                    var resultList = findResult.ToList();
                    int sentBatchCount = 0;
                    foreach (var queuedReq in resultList)
                    {
                        var balance = await wc.RefreshBalance(netName);
                        var cb = balance["Lyra.LeX"];
                        Console.WriteLine($"Current Lyra.LeX Balance: {cb}");
                        if (cb > 202)
                        {
                            Console.Write($"Sending to {queuedReq.UserName} <{queuedReq.Email}>");
                            try
                            {
                                await wc.Transfer("Lyra.LeX", queuedReq.AccountID, 200);
                                updateDef = updateDef.Set(o => o.State, 1);
                            }
                            catch (Exception tex)
                            {
                                if (tex.Message == "InvalidDestinationAccountId")
                                {
                                    updateDef = updateDef.Set(o => o.State, 2);
                                }
                                else
                                {
                                    updateDef = updateDef.Set(o => o.State, 3);
                                }
                            }
                            // send result: 1=success; 2=InvalidDestinationAccountId; 3=unknown error
                            queuedReq.SentTime = DateTime.Now;
                            var tgtReqFilter = Builders<FreeLeXRequest>.Filter.Eq(x => x.AccountID, queuedReq.AccountID);
                            await lexReqs.UpdateOneAsync(tgtReqFilter, updateDef);
                            sentBatchCount++;
                            Console.WriteLine(" Done.");
                        }
                    }
                    if (sentBatchCount > 0)
                        continue;
                    else
                    {
                        Console.Write(".");
                        await Task.Delay(10000);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    await Task.Delay(30000);
                }
            }
        }
    }
}
