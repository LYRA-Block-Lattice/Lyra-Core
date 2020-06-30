using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Lyra.Core.Blocks;
using System.Collections.Generic;
using Lyra.Core.API;

namespace Lyra.Node.Test
{
    public partial class Authorizer_Tests
    {
        TokenGenesisBlock _DiscountTokenBlock = null;

        //int DISC_PRECISION = 2;


        [TestMethod]
        public void CreateNonFungibleToken()
        {
            CreateFirstGenesisBlock();

            var result = DiscountTokenGenesis();

            Assert.AreEqual(APIResultCodes.Success, result);

            var previous_GRFT_balance = _FirstGenesisBlock.Balances[LyraGlobal.OFFICIALTICKERCODE];
            var new_GRFT_balance = _DiscountTokenBlock.Balances[LyraGlobal.OFFICIALTICKERCODE];

            Assert.AreEqual(0, previous_GRFT_balance - (new_GRFT_balance + serviceAccount.GetLastServiceBlock().TokenGenerationFee));

            Assert.AreEqual(1799999900, _DiscountTokenBlock.Balances[LyraGlobal.OFFICIALTICKERCODE]);
            Assert.AreEqual(100000, _DiscountTokenBlock.Balances["Custom.DISC"]);

        }

        // this is suppsoed to be the good one
        [TestMethod]
        public void SendNonFungibleToken_1()
        {
            CreateFirstGenesisBlock();

            DiscountTokenGenesis();

            var disount_token = new NonFungibleToken();
            disount_token.Denomination = 50;
            disount_token.TokenCode = "Custom.DISC";
            disount_token.RedemptionCode = "12345";
            disount_token.ExpirationDate = DateTime.Now + TimeSpan.FromDays(365); // one year
            disount_token.SerialNumber = disount_token.CalculateHash(); // Just set to some random string
            disount_token.Sign(PrivateKey1);

            var result = SendDiscountToken(disount_token);

            Assert.AreEqual(APIResultCodes.Success, result);

            var previous_GRFT_balance = _DiscountTokenBlock.Balances[LyraGlobal.OFFICIALTICKERCODE];
            var new_GRFT_balance = _SendTransferBlock.Balances[LyraGlobal.OFFICIALTICKERCODE];

            var previous_DISC_balance = _DiscountTokenBlock.Balances["Custom.DISC"];
            var new_DISC_balance = _SendTransferBlock.Balances["Custom.DISC"];

            Assert.AreEqual(0, previous_GRFT_balance - new_GRFT_balance - serviceAccount.GetLastServiceBlock().TransferFee);
            Assert.AreEqual(0, previous_DISC_balance - new_DISC_balance - 50);

            Assert.AreEqual(1799999899, _SendTransferBlock.Balances[LyraGlobal.OFFICIALTICKERCODE]);
            Assert.AreEqual(99950, _SendTransferBlock.Balances["Custom.DISC"]);

        }

        // should return error as the token is marked as NonFungible and there is no non fungible in the block
        [TestMethod]
        public void SendNonFungibleToken_2()
        {
            CreateFirstGenesisBlock();

            DiscountTokenGenesis();

            var result = SendDiscountToken(null);

            Assert.AreEqual(APIResultCodes.MissingNonFungibleToken, result);

        }

        [TestMethod]
        public void SendNonFungibleToken_3()
        {
            CreateFirstGenesisBlock();

            DiscountTokenGenesis();

            var disount_token = new NonFungibleToken();
            disount_token.Denomination = 1;
            //disount_token.OriginHash = "some unique random string";
            disount_token.TokenCode = "Custom.DISC";
            disount_token.RedemptionCode = "12345";

            var result = SendDiscountToken(disount_token);

            Assert.AreEqual(APIResultCodes.InvalidNonFungibleAmount, result);

        }

        [TestMethod]
        public void SendNonFungibleToken_4_WRONG_TOKEN_CODE()
        {
            CreateFirstGenesisBlock();

            DiscountTokenGenesis();

            var disount_token = new NonFungibleToken();
            disount_token.Denomination = 50;
            disount_token.TokenCode = LyraGlobal.OFFICIALTICKERCODE;
            //disount_token.OriginHash = "some unique random string";
            disount_token.RedemptionCode = "12345";

            var result = SendDiscountToken(disount_token);

            Assert.AreEqual(APIResultCodes.InvalidNonFungibleTokenCode, result);

        }

        [TestMethod]
        public void SendNonFungibleToken_5()
        {
            CreateFirstGenesisBlock();

            DiscountTokenGenesis();

            var disount_token = new NonFungibleToken();
            disount_token.Denomination = 50;
            disount_token.TokenCode = "Custom.DISC";
            disount_token.RedemptionCode = "12345";
            // disount_token.OriginHash = "some unique random string";

            var result = SendDiscountToken(disount_token);

            Assert.AreEqual(APIResultCodes.NonFungibleSignatureVerificationFailed, result);

        }

        [TestMethod]
        public void ReceiveNonFungibleToken_1()
        {
            CreateFirstGenesisBlock();

            DiscountTokenGenesis();

            var disount_token = new NonFungibleToken();
            disount_token.Denomination = 50;
            disount_token.TokenCode = "Custom.DISC";
            //  disount_token.OriginHash = "some unique random string";
            disount_token.RedemptionCode = "12345";

            // THis will fail as the token is not signed
            SendDiscountToken(disount_token);

            var result = ProcessReceiveDiscountToken(null);

            Assert.AreEqual(APIResultCodes.SourceSendBlockNotFound, result);

            Assert.AreEqual(50, _OpenAccount2Block.Balances["Custom.DISC"]);
        }

        [TestMethod]
        public void ReceiveNonFungibleToken_2()
        {
            CreateFirstGenesisBlock();

            DiscountTokenGenesis();

            var disount_token = new NonFungibleToken();
            disount_token.Denomination = 50;
            disount_token.TokenCode = "Custom.DISC";
            //  disount_token.OriginHash = "some unique random string";
            disount_token.RedemptionCode = "12345";
            disount_token.Sign(PrivateKey1);

            SendDiscountToken(disount_token);

            disount_token.Sign(PrivateKey2);

            var result = ProcessReceiveDiscountToken(disount_token);

            Assert.AreEqual(APIResultCodes.NonFungibleSignatureVerificationFailed, result);

            Assert.AreEqual(50, _OpenAccount2Block.Balances["Custom.DISC"]);
        }

        [TestMethod]
        public void ReceiveNonFungibleToken_3()
        {
            CreateFirstGenesisBlock();

            DiscountTokenGenesis();

            var disount_token = new NonFungibleToken();
            disount_token.Denomination = 50;
            disount_token.TokenCode = "Custom.DISC";
            //  disount_token.OriginHash = "some unique random string";
            disount_token.RedemptionCode = "12345678901234567890";
            disount_token.Sign(PrivateKey1);

            SendDiscountToken(disount_token);

            var result = ProcessReceiveDiscountToken(disount_token);

            Assert.AreEqual(APIResultCodes.Success, result);

            Assert.AreEqual(50, _OpenAccount2Block.Balances["Custom.DISC"]);
        }

        [TestMethod]
        [ExpectedException(typeof(System.Security.Cryptography.CryptographicException))]
        public void ReceiveNonFungibleToken_4()
        {
            CreateFirstGenesisBlock();

            DiscountTokenGenesis();

            var disount_token = new NonFungibleToken();
            disount_token.Denomination = 50;
            disount_token.TokenCode = "Custom.DISC";
            //  disount_token.OriginHash = "some unique random string";
            disount_token.RedemptionCode = "12345678901234567890";
            disount_token.Sign(PrivateKey1);

            SendDiscountToken(disount_token);

            ProcessReceiveDiscountToken(disount_token);

            var encryptor = new ECC_DHA_AES_Encryptor();

            string decrypted_redemption_code = encryptor.Decrypt(PrivateKey2, AccountId1, "54CRv2pEjNj8c3UBPv4AxYmEKEojmVdAagTpgSjHieAq", "54CRv2pEjNj8c3UBPv4AxYmEKEojmVdAagTpgSjHieAq");

            //Assert.AreEqual("12345678901234567890", decrypted_redemption_code);
        }

        [TestMethod]
        public void ReceiveNonFungibleToken_5()
        {
            CreateFirstGenesisBlock();

            DiscountTokenGenesis();

            var encryptor = new ECC_DHA_AES_Encryptor();

            var discount_token = new NonFungibleToken();
            discount_token.Denomination = 50;
            discount_token.TokenCode = "Custom.DISC";
            //  disount_token.OriginHash = "some unique random string";
            discount_token.ExpirationDate = DateTime.Now + TimeSpan.FromDays(365);
            discount_token.SerialNumber = discount_token.CalculateHash(); // Just set to some random string
            discount_token.RedemptionCode = encryptor.Encrypt(PrivateKey1, AccountId2, discount_token.SerialNumber, "12345678901234567890");
            discount_token.Sign(PrivateKey1);

            SendDiscountToken(discount_token);

            ProcessReceiveDiscountToken(discount_token);

            var decryptor = new ECC_DHA_AES_Encryptor();

            string decrypted_redemption_code = decryptor.Decrypt(PrivateKey2, AccountId1, _OpenAccount2Block.NonFungibleToken.SerialNumber, (_OpenAccount2Block.NonFungibleToken as NonFungibleToken).RedemptionCode);

            Assert.AreEqual("12345678901234567890", decrypted_redemption_code);
        }

        APIResultCodes ProcessReceiveDiscountToken(NonFungibleToken discount_token)
        {
            var authorizer = new NewAccountAuthorizer(serviceAccount, accountCollection);
            var block = CreateReceiveDiscountBlockOnAcc2(discount_token);
            return authorizer.Authorize<OpenWithReceiveTransferBlock>(ref block);
        }

        OpenWithReceiveTransferBlock CreateReceiveDiscountBlockOnAcc2(NonFungibleToken discount_token)
        {
            _OpenAccount2Block = new OpenWithReceiveTransferBlock
            {
                AccountID = AccountId2,
                AccountType = AccountTypes.Standard,
                ServiceHash = string.Empty,
                SourceHash = _SendTransferBlock.Hash,
                Fee = 0,
                FeeType = AuthorizationFeeTypes.NoFee,
                Balances = new Dictionary<string, decimal>()
            };

            _OpenAccount2Block.Balances.Add("Custom.DISC", 50);

            if (discount_token != null)
            {
                _OpenAccount2Block.NonFungibleToken = discount_token;
            }

            _OpenAccount2Block.InitializeBlock(null, PrivateKey2, NETWORK_ID);

            // _OpenAccountBlock.Signature = Signatures.GetSignature(PrivateKey2, _OpenAccountBlock.Hash);

            return _OpenAccount2Block;
        }

        APIResultCodes SendDiscountToken(NonFungibleToken disount_token)
        {
            _SendTransferBlock = CreateSendDiscountTokenBlock(disount_token, _DiscountTokenBlock);
            var authorizer = new SendTransferAuthorizer(serviceAccount, accountCollection);

            return authorizer.Authorize<SendTransferBlock>(ref _SendTransferBlock);
        }

        APIResultCodes DiscountTokenGenesis()
        {
            _DiscountTokenBlock = CreateDiscountTokenGenesisBlock(_FirstGenesisBlock);
            var authorizer = new NewTokenAuthorizer(serviceAccount, accountCollection);

            return authorizer.Authorize<TokenGenesisBlock>(ref _DiscountTokenBlock);
        }

        TokenGenesisBlock CreateDiscountTokenGenesisBlock(TransactionBlock previousBlock)
        {
            var TokenGenerationFee = serviceAccount.GetLastServiceBlock().TokenGenerationFee;

            TokenGenesisBlock tokenBlock = new TokenGenesisBlock
            {
                Ticker = "Custom.DISC",
                DomainName = "Custom",
                Description = "DISCOUNT NONFUNGIBLE TOKEN TEST",
                Precision = 2,
                IsFinalSupply = false,
                AccountID = AccountId1,
                Balances = new Dictionary<string, decimal>(),
                ServiceHash = string.Empty,
                Fee = TokenGenerationFee,
                FeeType = AuthorizationFeeTypes.Regular,
                IsNonFungible = true,
                NonFungibleKey = AccountId1,
                NonFungibleType = NonFungibleTokenTypes.LoyaltyDiscount,
                RenewalDate = DateTime.Now.Add(TimeSpan.FromDays(365)),
            };

            var transaction = new TransactionInfo() { TokenCode = "Custom.DISC", Amount = 100000 };

            tokenBlock.Balances.Add(transaction.TokenCode, transaction.Amount); // This is current supply in atomic units (1,000,000.00)
            tokenBlock.Balances.Add(LyraGlobal.OFFICIALTICKERCODE, previousBlock.Balances[LyraGlobal.OFFICIALTICKERCODE] - TokenGenerationFee);

            tokenBlock.InitializeBlock(previousBlock, PrivateKey1, NETWORK_ID);

            //tokenBlock.Signature = Signatures.GetSignature(PrivateKey1, tokenBlock.Hash);

            return tokenBlock;
        }

        SendTransferBlock CreateSendDiscountTokenBlock(NonFungibleToken discount_token, TransactionBlock previousBlock)
        {
            decimal TransferFee = serviceAccount.GetLastServiceBlock().TransferFee;

            sbyte precision = _FirstGenesisBlock.Precision;

            //long atomicamount = (long)(50 * Math.Pow(10, precision));

            SendTransferBlock sendBlock = new SendTransferBlock
            {
                AccountID = AccountId1,
                ServiceHash = string.Empty,
                DestinationAccountId = AccountId2,
                Balances = new Dictionary<string, decimal>(),
                Fee = TransferFee,
                FeeType = AuthorizationFeeTypes.Regular,
                FeeCode = LyraGlobal.OFFICIALTICKERCODE
            };

            sendBlock.Balances.Add(LyraGlobal.OFFICIALTICKERCODE, previousBlock.Balances[LyraGlobal.OFFICIALTICKERCODE] - TransferFee);
            sendBlock.Balances.Add("Custom.DISC", previousBlock.Balances["Custom.DISC"] - 50);

            if (discount_token != null)
            {
                //sendBlock.NonFungibleTokens = new List<INonFungibleToken>();
                //sendBlock.NonFungibleTokens.Add(discount_token);
                sendBlock.NonFungibleToken = discount_token;

                //if (discount_token.OriginHash == null)
                //    discount_token.OriginHash = sendBlock.CalculateHash();
            }


            sendBlock.InitializeBlock(previousBlock, PrivateKey1, NETWORK_ID);

            //sendBlock.Signature = Signatures.GetSignature(PrivateKey1, sendBlock.Hash);

            return sendBlock;

        }


    }
}
