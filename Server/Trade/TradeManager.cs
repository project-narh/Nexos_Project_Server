using Nethereum.Hex.HexConvertors.Extensions;
using Org.BouncyCastle.Crypto;
using Server.BlockChain;
using Server.Database;
using Server.Packet;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

namespace Server.Trade
{
    public class TradeManager
    {
        public static TradeManager Instance { get; } = new TradeManager();

        private Dictionary<(int,int), Trade> tradeList = new Dictionary<(int, int), Trade>();

        public bool AddTrade(int sender, int receiver)
        {
            if (!tradeList.ContainsKey((sender, receiver)))
            {
                Trade trade = new Trade(sender, receiver);
                tradeList.Add((sender, receiver), trade);
                Console.WriteLine($"[거래] {sender} -> {receiver} : 거래 생성");
                return true;
            }   
            else
            {
                Console.WriteLine($"[거래] {sender} -> {receiver} : 생성 실패 - 이미 거래가 존재");
                return false;
            }
        }

        public async Task<(bool,bool)> TradeConfirm(int sender, int receiver, int uid, bool isConfirm, string sig)
        {
            if(isConfirm)
            {
                if (tradeList.ContainsKey((sender, receiver)))
                {
                    Console.WriteLine($"[거래] {sender} -> {receiver} 찾음.");
                    Trade trade = tradeList[(sender, receiver)];
                    if(isConfirm)
                        return await trade.TradeConfirm(uid,sig);
                }
                else
                {
                    Console.WriteLine($"[거래] {sender} -> {receiver} 를 찾을 수 없습니다.");
                }
            }
            return (false, false);
        }

        public void RemoveTrade(int sender, int receiver)
        {
            if (tradeList.ContainsKey((sender, receiver)))
            {
                tradeList.Remove((sender, receiver));
                Console.WriteLine($"[거래] {sender} -> {receiver} : 거래 제거 완료");
            }
            else
            {
                Console.WriteLine($"[거래] {sender} -> {receiver} : 존재하지 않습니다.");
            }
        }

        public bool AcceptTrade(int sender, int receiver, bool isAccept, int uid)
        {
            if (tradeList.ContainsKey((sender, receiver)))
            {
                if(isAccept)
                {
                    Trade trade = tradeList[(sender, receiver)];
                    if(trade.TradeAccept(uid))
                    {
                        //거래 성공
                        return true;
                    }
                    else
                    {
                        //아직 성공은 아님
                        return false;
                    }
                }
                else
                {
                    RemoveTrade(sender, receiver);
                    Console.WriteLine($"[거래] {sender} -> {receiver} : 거래가 취소되었습니다.");
                }
            }
            else
            {
                Console.WriteLine($"[거래] {sender} -> {receiver} : 거래가 존재하지 않아 취소되었습니다");
            }
            return false;
        }

        public bool Cal_Data(int sender, int receiver, int id, TradeItem item, bool isIncreased)
        {
            if (tradeList.ContainsKey((sender, receiver)))
            {
                return tradeList[(sender, receiver)].DataSend(id, item, isIncreased);
            }
            else
            {
                Console.WriteLine($"[거래] {sender} -> {receiver} : 거래가 존재하지 않아 취소되었습니다");
                return false;
            }
        }

        public async Task<bool> ChainComplete(string sender, string receiver)
        {
            int senderUid = await DBManager.Instance.GetUIDByAddress(sender) ?? -1;
            int receiverUid = await DBManager.Instance.GetUIDByAddress(receiver) ?? -1;

            if (senderUid == -1 || receiverUid == -1)
            {
                Console.WriteLine("[거래] UID 조회 실패");
                return false;
            }

            Trade trade = null;
            bool isComplete = false;
            if (tradeList.TryGetValue((senderUid, receiverUid), out trade))
            {
                isComplete = await trade.ChainComplete(senderUid);
            }
            else if(tradeList.TryGetValue((receiverUid, senderUid), out trade))
            {
                isComplete = await trade.ChainComplete(receiverUid);
            }
            else
            {
                Console.WriteLine("[거래] 거래가 존재하지 않습니다.");
                return false;
            }
            return true;
        }

    }

    public class Trade
    {
        private int senderId;
        private int receiverId;
        private string senderAddress;
        private string receiverAddress;
        private bool isReceiverAccept;
        private bool isTrade;
        private bool[] isTradeAccept = new bool[2] { false, false };
        private bool[] isChain = new bool[2] { false, false };
        List<TradeItem> senderItems = new List<TradeItem>();
        List<TradeItem> receiverItems = new List<TradeItem>();
        private bool isTradeStarted = false;
        private string senderSignature;
        private string receiverSignature;
        private object _lock = new object();

        public Trade(int senderId, int receiverId)
        {
            this.senderId = senderId;
            this.receiverId = receiverId;
        }

        public async Task<bool> ChainComplete(int from)// 누가 보냈는지
        {
            if (from == senderId )
            {
                if (!isChain[0])
                {
                    isChain[0] = true;
                }
                else
                {
                    Console.WriteLine("[거래] 블록체인이 똑같은걸 두번 접근 Error");
                }
            }
            else if(from == receiverId)
            {
                if (!isChain[1])
                {
                    isChain[1] = true;
                }
                else
                {
                    Console.WriteLine("[거래] 블록체인이 똑같은걸 두번 접근 Error");
                }
            }
            else
            {
                return false;
            }

            if (isChain[0] == true && isChain[1] == true)
            {
                ResetTradeState();
                Console.WriteLine("[거래] 블록체인 거래가 완료되었습니다. 아이템 상태를 변경합니다.");

                foreach (var item in senderItems)
                {
                    await DBManager.Instance.Update_Item(item.itemId, item.uniqueId, ItemState.USE);
                }

                foreach (var item in receiverItems)
                {
                    await DBManager.Instance.Update_Item(item.itemId, item.uniqueId, ItemState.USE);
                }
                Console.WriteLine("[거래] 아이템 상태를 사용 가능으로 변경하였습니다.");
                return true;
            }
            return false;
        }

        public bool GetTrade()
        {
            return isTrade;
        }

        public bool DataSend(int id, TradeItem item, bool isIncreased)
        {
            if (isTrade)
            {
                List<TradeItem> temp;
                if (senderId == id)
                {
                    //거래자
                    temp = senderItems;
                }
                else if (receiverId == id)
                {
                    //생성자
                    temp = receiverItems;
                }
                else
                {
                    Console.WriteLine("[거래] 잘못된 ID");
                    return false;
                }

                int index = temp.FindIndex(x => x.itemId == item.itemId && x.uniqueId == item.uniqueId);
                if (index >= 0)
                {
                    var titem = temp[index];

                    if (isIncreased)
                    {
                        titem.count += item.count;
                    }
                    else
                    {
                        int result = titem.count - item.count;
                        if (result < 0)
                        {
                            Console.WriteLine("[거래] 아이템 수량이 부족합니다.");
                            return false;
                        }
                        else if (result == 0)
                        {
                            temp.RemoveAt(index);
                            return true;
                        }
                        titem.count = result;
                    }

                    temp[index] = titem;
                }
                else
                {
                    if (isIncreased)
                    {
                        temp.Add(item);
                    }
                    else
                    {
                        Console.WriteLine("[거래] 존재하지 않는 아이템");
                        return false;
                    }
                }
            }
            else
            {
                return false;
            }

            return true;
        }

        //public async Task<(bool isSuccess, bool isCompleted)> TradeConfirm(int uid) // 성공, 거래 완료
        //{
        //    if (!isReceiverAccept) return (false,false);
        //    if (uid == senderId)
        //    {
        //        isTradeAccept[0] = true;
        //        isTrade = false;
        //        Console.WriteLine($"[거래] Sender 수락");
        //    }
        //    else if (uid == receiverId)
        //    {
        //        isTradeAccept[1] = true;
        //        isTrade = false;
        //        Console.WriteLine($"[거래] receiver 수락");
        //    }

        //    if (isTradeAccept[0] && isTradeAccept[1])
        //    {
        //        if(!isTradeStarted)
        //        {
        //            bool isTradeResult = await TradeStart();
        //            if (!isTradeResult) return (false, true);
        //            return (true, true);
        //        }
        //    }

        //    Console.WriteLine($"[거래] 한쪽이 수락을 진행하였습니다.");
        //    return (true,false);
        //}

         public async Task<(bool isSuccess, bool isCompleted)> TradeConfirm(int uid, string signature) // 성공, 거래 완료
        {
            if (!isReceiverAccept) return (false,false);
            if (uid == senderId)
            {
                isTradeAccept[0] = true;
                senderSignature = signature;
                isTrade = false;
                Console.WriteLine($"[거래] Sender 수락");
            }
            else if (uid == receiverId)
            {
                isTradeAccept[1] = true;
                receiverSignature = signature;
                isTrade = false;
                Console.WriteLine($"[거래] receiver 수락");
            }

            if (isTradeAccept[0] && isTradeAccept[1])
            {
                if(!isTradeStarted)
                {
                    bool isTradeResult = await TradeStart();
                    if (!isTradeResult) return (false, true);
                    return (true, true);
                }
            }

            Console.WriteLine($"[거래] 한쪽이 수락을 진행하였습니다.");
            return (true,false);
        }

        public bool TradeAccept(int uid) // return : 거래 성공 여부
        {
            if(!isReceiverAccept)
            {
                if(uid == receiverId)
                {
                    Console.WriteLine("[거래] 거래가 수락되었습니다.");
                    isTrade = true;
                    isReceiverAccept = true;
                    return true;
                }
                else
                {
                    Console.WriteLine("[거래] error");
                }
            }
            else
            {
                Console.WriteLine("[거래] 현재 이미 수락되어 있습니다.");
            }
            return false;
        }

        public async Task<bool> TradeStart()
        {
            Console.WriteLine("[거래] 모든 플레이어가 수락하여 교환이 시작됩니다.");
            var _senderTokenIds = senderItems.Select(x => x.tokenId).ToList();
            var _receiverTokenIds = receiverItems.Select(x => x.tokenId).ToList();

            var _senderCounts = senderItems.Select(x => x.count).ToList();
            var _receiverCounts = receiverItems.Select(x => x.count).ToList();

            var senderTokenIds = _senderTokenIds.Select(x => new System.Numerics.BigInteger(x)).ToList();
            var receiverTokenIds = _receiverTokenIds.Select(x => new System.Numerics.BigInteger(x)).ToList();
            var senderCounts = _senderCounts.Select(x => new System.Numerics.BigInteger(x)).ToList();
            var receiverCounts = _receiverCounts.Select(x => new System.Numerics.BigInteger(x)).ToList();

            var user1 = await DBManager.Instance.GetAddressByUID(senderId);
            var user2 = await DBManager.Instance.GetAddressByUID(receiverId);

            Console.WriteLine("[거래] 전송할 TokenId 및 Count 목록");

            Console.WriteLine($"User1 : {senderId}  - {user1.pr_Address}");
            Console.WriteLine($"User2 : {receiverId}  - {user2.pr_Address}");

            Console.WriteLine($"Sender");
            for (int i = 0; i < _senderTokenIds.Count; i++)
            {
                Console.WriteLine($"  TokenId: {senderTokenIds[i]}, Count: {senderCounts[i]}");
            }

            Console.WriteLine($"Receiver");
            for (int i = 0; i < _receiverTokenIds.Count; i++)
            {
                Console.WriteLine($"  TokenId: {receiverTokenIds[i]}, Count: {receiverCounts[i]}");
            }
            Console.WriteLine($"User 1 - sign {senderSignature.HexToByteArray().ToHex()}");
            Console.WriteLine($"User 2 - sign {receiverSignature.HexToByteArray().ToHex()}");

            TradeItemsByServerFunction_Sign txData = new TradeItemsByServerFunction_Sign
            {
                TradeInfo = new TradeInfoDTO
                {
                    User1 = user1.pr_Address,
                    User2 = user2.pr_Address,
                    User1TokenIds = senderTokenIds,
                    User1Amounts = receiverTokenIds,
                    User2TokenIds = senderCounts,
                    User2Amounts = receiverCounts,
                    User1Sig = senderSignature.HexToByteArray(),
                    User2Sig = receiverSignature.HexToByteArray()
                },
                User1Sign = user1.address,
                User2Sign = user2.address
            };

            //var results = await BlockChainManager.Instance.TradeItem(txData, user1.address, user2.address);
            var results = await BlockChainManager.Instance.TradeItemRawABI_Compatible(txData);

            if (results)
            {
                Console.WriteLine("[거래] NFT 거래가 성공적으로 진행되었습니다.");
                var trade = await DBManager.Instance.Trade_Items_Swap(senderId, receiverId, senderItems, receiverItems);
                if (trade)
                {
                    Console.WriteLine("[거래] 교환이 성공적으로 완료되었습니다.");

                    return true;
                }
                Console.WriteLine("[거래] 교환이 실패하였습니다.");

                return false;
            }
            else
            {
                Console.WriteLine("[거래] 블록체인 실패");
                Console.WriteLine("[거래] 거래 실패.");
                return false;
            }
        }

        //public async Task<bool> TradeStart()
        //{
        //    Console.WriteLine("[거래] 모든 플레이어가 수락하여 교환이 시작됩니다.");
        //    var senderTokenIds = senderItems.Select(x => x.tokenId).ToList();
        //    var receiverTokenIds = receiverItems.Select(x => x.tokenId).ToList();

        //    var senderCounts = senderItems.Select(x => x.count).ToList();
        //    var receiverCounts = receiverItems.Select(x => x.count).ToList();

        //    Console.WriteLine("[거래] 전송할 TokenId 및 Count 목록");

        //    Console.WriteLine($"Sender : {senderId} TokenIds and Counts:");
        //    for (int i = 0; i < senderTokenIds.Count; i++)
        //    {
        //        Console.WriteLine($"  TokenId: {senderTokenIds[i]}, Count: {senderCounts[i]}");
        //    }

        //    Console.WriteLine($"Receiver : {receiverId} TokenIds and Counts:");
        //    for (int i = 0; i < receiverTokenIds.Count; i++)
        //    {
        //        Console.WriteLine($"  TokenId: {receiverTokenIds[i]}, Count: {receiverCounts[i]}");
        //    }


        //    var blocksenderTask = BlockChainManager.Instance.TradeItem(senderId, receiverId, senderTokenIds, senderCounts);
        //    var blockReceiverTask = BlockChainManager.Instance.TradeItem(receiverId, senderId, receiverTokenIds, receiverCounts);

        //    var results = await Task.WhenAll(blocksenderTask, blockReceiverTask);

        //    if (results.All(x => x))
        //    {
        //        Console.WriteLine("[거래] NFT 거래가 성공적으로 진행되었습니다.");
        //        var trade = await DBManager.Instance.Trade_Items_Swap(senderId, receiverId, senderItems, receiverItems);
        //        if (trade)
        //        {
        //            Console.WriteLine("[거래] 교환이 성공적으로 완료되었습니다.");

        //            return true;
        //        }
        //        Console.WriteLine("[거래] 교환이 실패하였습니다.");

        //        return false;
        //    }
        //    else
        //    {
        //        Console.WriteLine("[거래] 블록체인 실패");
        //        Console.WriteLine("[거래] 거래 실패.");
        //        return false;
        //    }
        //}

        private void ResetTradeState()
        {
            isReceiverAccept = false;
            isTrade = false;
            isTradeAccept = new bool[2] { false, false };
            senderItems.Clear();
            receiverItems.Clear();
        }


    }
}
