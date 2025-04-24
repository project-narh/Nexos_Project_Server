using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using System.Numerics;

namespace Server.BlockChain
{
    public class ItemClass
    {
    }

    #region Mint
    public interface IMint
    {
        string to { get; set; }
        int itemId { get; set; }
        int serial { get; set; }
        int amount { get; set; }
        byte[] dataHash { get; set; }
    }

    [Function("mintToUser", "uint256")]
    public class MintitemFunction : FunctionMessage, IMint
    {
        [Parameter("address", "to", 1)]
        public string to { get; set; }

        [Parameter("uint256", "itemId", 2)]
        public int itemId { get; set; }

        [Parameter("uint256", "serial", 3)]
        public int serial { get; set; }

        [Parameter("uint256", "amount", 4)]
        public int amount { get; set; }

        [Parameter("bytes32", "dataHash", 5)]
        public byte[] dataHash { get; set; }

        public MintitemFunction() { }

        public MintitemFunction(MintitemFunction_sign sign)
        {
            to = sign.to;
            itemId = sign.itemId;
            serial = sign.serial;
            amount = sign.amount;
            dataHash = sign.dataHash;
        }
    }

    [Function("mintToUser", "uint256")]
    public class MintitemFunction_sign : FunctionMessage, IMint
    {
        [Parameter("address", "to", 1)]
        public string to { get; set; }

        [Parameter("uint256", "itemId", 2)]
        public int itemId { get; set; }

        [Parameter("uint256", "serial", 3)]
        public int serial { get; set; }

        [Parameter("uint256", "amount", 4)]
        public int amount { get; set; }

        [Parameter("bytes32", "dataHash", 5)]
        public byte[] dataHash { get; set; }

        [Parameter("bytes", "signature", 6)]
        public byte[] signature { get; set; }
    }

    [Event("ItemMinted")]
    public class ItemMintDTO : IEventDTO // 스마트 컨트랙트 이벤트
    {
        [Parameter("uint256", "tokenId", 1)]
        public int tokenId { get; set; }

        [Parameter("uint256", "itemId", 2)]
        public int itemId { get; set; }

        [Parameter("uint256", "serial", 3)]
        public int serial { get; set; }

        [Parameter("uint256", "version", 4)]
        public int version { get; set; }
    }
    #endregion

    #region Burn
    [Function("burnItem_Server")]
    public class ItemBurnFunction : FunctionMessage
    {
        [Parameter("address", "from", 1)]
        public string from { get; set; }

        [Parameter("uint256", "tokenId", 2)]
        public int tokenId { get; set; }

        [Parameter("uint256", "amount", 3)]
        public int amount { get; set; }
    }

    [Event("ItemBurned")]
    public class ItemBurnDTO : IEventDTO
    {
        [Parameter("uint256", "tokenId", 1, false)]
        public int tokenId { get; set; }

        [Parameter("uint256", "itemId", 2, false)]
        public int itemId { get; set; }

        [Parameter("uint256", "serial", 3, false)]
        public int serial { get; set; }
    }
    #endregion

    #region 거래

    [Function("safeBatchTransferFrom")]
    public class SafeBatchTransferFromFunction : FunctionMessage
    {
        [Parameter("address", "from", 1)]
        public string from { get; set; }

        [Parameter("address", "to", 2)]
        public string to { get; set; }

        [Parameter("uint256[]", "ids", 3)]
        public List<int> ids { get; set; }

        [Parameter("uint256[]", "amounts", 4)]
        public List<int> amounts { get; set; }

        [Parameter("bytes", "data", 5)]
        public byte[] data { get; set; } = new byte[0];
    }

    [Event("ItemTransferred")]
    public class ItemTransferredDTO : IEventDTO
    {
        [Parameter("address", "from", 1, true)]
        public string from { get; set; }

        [Parameter("address", "to", 2, true)]
        public string to { get; set; }

        [Parameter("uint256[]", "ids", 3, false)]
        public List<BigInteger> ids { get; set; }

        [Parameter("uint256[]", "amounts", 4, false)]
        public List<BigInteger> amounts { get; set; }
    }

    [Function("tradeItemsByServer")]
    public class TradeItemsByServerFunction : FunctionMessage
    {
        [Parameter("address", "user1", 1)]
        public string User1 { get; set; }

        [Parameter("address", "user2", 2)]
        public string User2 { get; set; }

        [Parameter("uint256[]", "user1TokenIds", 3)]
        public List<BigInteger> User1TokenIds { get; set; }

        [Parameter("uint256[]", "user1Amounts", 4)]
        public List<BigInteger> User1Amounts { get; set; }

        [Parameter("uint256[]", "user2TokenIds", 5)]
        public List<BigInteger> User2TokenIds { get; set; }

        [Parameter("uint256[]", "user2Amounts", 6)]
        public List<BigInteger> User2Amounts { get; set; }

        public TradeItemsByServerFunction() { }

        public TradeItemsByServerFunction(TradeItemsByServerFunction_Sign sign)
        {
            User1 = sign.TradeInfo.User1;
            User2 = sign.TradeInfo.User2;
            User1TokenIds = sign.TradeInfo.User1TokenIds;
            User1Amounts = sign.TradeInfo.User1Amounts;
            User2TokenIds = sign.TradeInfo.User2TokenIds;
            User2Amounts = sign.TradeInfo.User2Amounts;
        }
    }

    [Struct("TradeInfo")]
    public class TradeInfoDTO
    {
        [Parameter("address", "user1", 1)]
        public string User1 { get; set; }

        [Parameter("address", "user2", 2)]
        public string User2 { get; set; }

        [Parameter("uint256[]", "user1TokenIds", 3)]
        public List<BigInteger> User1TokenIds { get; set; }

        [Parameter("uint256[]", "user1Amounts", 4)]
        public List<BigInteger> User1Amounts { get; set; }

        [Parameter("uint256[]", "user2TokenIds", 5)]
        public List<BigInteger> User2TokenIds { get; set; }

        [Parameter("uint256[]", "user2Amounts", 6)]
        public List<BigInteger> User2Amounts { get; set; }

        [Parameter("bytes", "user1Sig", 7)]
        public byte[] User1Sig { get; set; }

        [Parameter("bytes", "user2Sig", 8)]
        public byte[] User2Sig { get; set; }
    }
    [Function("tradeItemsByServer", "bool")]
    public class TradeItemsByServerFunction_Sign : FunctionMessage
    {
        [Parameter("TradeInfo", "tradeInfo", 1)]
        public TradeInfoDTO TradeInfo { get; set; }

        [Parameter("address", "user1Sign", 2)]
        public string User1Sign { get; set; }

        [Parameter("address", "user2Sign", 3)]
        public string User2Sign { get; set; }
    }
    //[Struct("TradeInfo")]
    //public class TradeInfoStruct
    //{
    //    [Parameter("address", "user1", 1)]
    //    public string User1 { get; set; }

    //    [Parameter("address", "user2", 2)]
    //    public string User2 { get; set; }

    //    [Parameter("uint256[]", "user1TokenIds", 3)]
    //    public List<BigInteger> User1TokenIds { get; set; }

    //    [Parameter("uint256[]", "user1Amounts", 4)]
    //    public List<BigInteger> User1Amounts { get; set; }

    //    [Parameter("uint256[]", "user2TokenIds", 5)]
    //    public List<BigInteger> User2TokenIds { get; set; }

    //    [Parameter("uint256[]", "user2Amounts", 6)]
    //    public List<BigInteger> User2Amounts { get; set; }

    //    [Parameter("bytes", "user1Sig", 7)]
    //    public byte[] User1Sig { get; set; }

    //    [Parameter("bytes", "user2Sig", 8)]
    //    public byte[] User2Sig { get; set; }
    //}
    //[Function("tradeItemsByServer")]
    //public class TradeItemsByServerFunction_Sign : FunctionMessage
    //{
    //    [Parameter("address", "user1", 1)]
    //    public string User1 { get; set; }

    //    [Parameter("address", "user2", 2)]
    //    public string User2 { get; set; }

    //    [Parameter("uint256[]", "user1TokenIds", 3)]
    //    public List<BigInteger> User1TokenIds { get; set; }

    //    [Parameter("uint256[]", "user1Amounts", 4)]
    //    public List<BigInteger> User1Amounts { get; set; }

    //    [Parameter("uint256[]", "user2TokenIds", 5)]
    //    public List<BigInteger> User2TokenIds { get; set; }

    //    [Parameter("uint256[]", "user2Amounts", 6)]
    //    public List<BigInteger> User2Amounts { get; set; }

    //    [Parameter("bytes", "user1Sig", 7)]
    //    public byte[] User1Sig { get; set; }

    //    [Parameter("bytes", "user2Sig", 8)]
    //    public byte[] User2Sig { get; set; }

    //    [Parameter("address", "user1Sign", 9)]
    //    public string User1Sign { get; set; }

    //    [Parameter("address", "user2Sign", 10)]
    //    public string User2Sign { get; set; }
    //}

    [Event("TradeSuccess")]
    public class TradeSuccessEventDTO : IEventDTO
    {
        [Parameter("address", "user1", 1, true)]
        public string User1 { get; set; }

        [Parameter("address", "user2", 2, true)]
        public string User2 { get; set; }

    }
    #endregion

    [Function("updateItemVersion")]
    public class UpdateItemVersionFunction : FunctionMessage
    {
        [Parameter("uint256", "tokenId", 1)]
        public int tokenId { get; set; }

        [Parameter("uint256", "newVersion", 2)]
        public int newVersion { get; set; }

        [Parameter("bytes32", "newDataHash", 3)]
        public byte[] newDataHash { get; set; }

        [Parameter("string", "reason", 4)]
        public string reason { get; set; }
    }

    [Function("updateItemVersion")]
    public class UpdateItemVersionFunction_sign : FunctionMessage
    {
        [Parameter("uint256", "tokenId", 1)]
        public int tokenId { get; set; }

        [Parameter("uint256", "newVersion", 2)]
        public int newVersion { get; set; }

        [Parameter("bytes32", "newDataHash", 3)]
        public byte[] newDataHash { get; set; }

        [Parameter("bytes", "signature", 4)]
        public byte[] signature { get; set; }

        [Parameter("string", "reason", 5)]
        public string reason { get; set; }
    }

    [Function("verifyItem", "bool")]
    public class VerifyItemFunction : FunctionMessage
    {
        [Parameter("uint256", "tokenId", 1)]
        public int tokenId { get; set; }

        [Parameter("bytes", "signature", 2)]
        public byte[] signature { get; set; }
    }

    [Function("balanceOf", "uint256")]
    public class BalanceOfFunction : FunctionMessage
    {
        [Parameter("address", "_owner", 1)]
        public string Owner { get; set; }

        [Parameter("uint256", "_id", 2)]
        public BigInteger TokenId { get; set; }
    }

    [Function("setApprovalForAll")]
    public class SetApprovalForAllFunction : FunctionMessage
    {
        [Parameter("address", "operator", 1)]
        public string Operator { get; set; }

        [Parameter("bool", "approved", 2)]
        public bool Approved { get; set; }
    }

    [Function("isApprovedForAll", "bool")]
    public class IsApprovedForAllFunction : FunctionMessage
    {
        [Parameter("address", "owner", 1)]
        public string Owner { get; set; }

        [Parameter("address", "operator", 2)]
        public string Operator { get; set; }
    }
    [Function("_gameServerAddress", "address")]
    public class GameServerAddressFunction : FunctionMessage
    {
    }
}
