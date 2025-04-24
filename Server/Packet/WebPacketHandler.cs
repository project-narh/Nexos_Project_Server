using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using MySqlX.XDevAPI.Common;
using Nethereum.Contracts.QueryHandlers.MultiCall;
using Nethereum.Hex.HexConvertors.Extensions;
using Server.BlockChain;
using Server.Database;
using Server.Trade;
using Server.Web;
using ServerCore;
using System;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml;
using System.Xml.Linq;

namespace Server.Packet
{
    public class WebPacketHandler
    {
        public static event Action<int, int>? _AddUser;
        public static event Func<int, Task<bool>>? _CheckUserAsync;

        #region 로그인 & 회원가입
        public static async Task<string> loginHandler(int uid, JsonElement data)
        {
            Console.WriteLine("실행");
            try
            {
                if (data.TryGetProperty("id", out JsonElement ide) && data.TryGetProperty("password", out JsonElement passworde))
                {
                    string id = ide.ToString() ?? "null";
                    string password = passworde.ToString() ?? "null";
                    (int UID, string name) = await DBManager.Instance.Login(id, password);
                    var response = new
                    {
                        login_response = new
                        {
                            uid = UID,
                            name = name
                        }
                    };

                    if (UID > 0)
                    {
                        _AddUser?.Invoke(uid, UID);
                        return JsonSerializer.Serialize(response, Startup.jsonOptions);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WebSocket] 로그인 에러 발생 : {ex.Message}");
            }
            var response_fail = new
            {
                login_response = new
                {
                    uid = -1,
                    name = "",
                }
            };
            return JsonSerializer.Serialize(response_fail, Startup.jsonOptions);
        }

        public static async Task<string> RegisterHandler(int uid, JsonElement data)
        {
            bool result;
            string msg = "회원가입중 오류가 발생했습니다";

            try
            {
                if (data.TryGetProperty("id", out JsonElement id) && data.TryGetProperty("password", out JsonElement password) && data.TryGetProperty("name", out JsonElement name))
                {

                    (result, msg) = await DBManager.Instance.RegisterAccount(id.ToString(), password.ToString(), name.ToString());
                    if (result) return JsonSerializer.Serialize(new
                    {
                        register_response = new
                        {
                            result = result,
                            msg = msg
                        }
                    }, Startup.jsonOptions);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WebSocket] 회원가입중 오류가 발생했습니다 : {ex.Message}");
            }
            return JsonSerializer.Serialize(new
            {
                register_response = new
                {
                    result = false,
                    msg = msg
                }
            }, Startup.jsonOptions);
        }
        #endregion

        public static async Task<string> inventory_loadHandler(int uid, JsonElement data)
        {
            List<UserItem> userItem;
            try
            {
                userItem = await DBManager.Instance.Get_UserItem(uid) ?? new List<UserItem>();
                Console.WriteLine($"[WebSocket] DB 조회 완료. 아이템 개수 = {userItem.Count}");
                return JsonSerializer.Serialize(new
                {
                    inventory_response = new
                    {
                        data = userItem?.Select(item => new
                        {
                            id = item.Id,
                            uid = item.Uid,
                            uniqueId = item.UniqueId,
                            name = item.Name,
                            description = item.Description,
                            state = item.State,
                            type = item.Type,
                            count = item.Count,
                            etc = item.Etc,
                            image = item.Image
                        }).ToList()
                    }
                }, Startup.jsonOptions);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WebSocket] DB 조회 중 예외 발생: {ex.Message}");
            }
            return JsonSerializer.Serialize(new
            {
                inventory_response = new
                { }
            }, Startup.jsonOptions);
        }
        /*
        public static async Task<string> item_createHandler(int uid, JsonElement data)
        {
            try
            {
                if (data.TryGetProperty("id", out JsonElement id) && data.TryGetProperty("count", out JsonElement count))
                {
                    bool result = await DBManager.Instance.Create_Item(uid, int.Parse(id.ToString()), int.Parse(count.ToString()));
                    if (result) return JsonSerializer.Serialize(new { success = "true" }, Startup.jsonOptions);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WebSocket] 아이템 생성 중 오류 발생 : {ex.Message}");
                return JsonSerializer.Serialize(new { success = "false" }, Startup.jsonOptions);
            }
            return JsonSerializer.Serialize(new { success = "false" }, Startup.jsonOptions);
        }
        */

        public static async Task<string> item_createHandler(int uid, JsonElement data) // 블록체인에 맞게 수정
        {
            try
            {
                if (data.TryGetProperty("id", out JsonElement id) && data.TryGetProperty("count", out JsonElement count))
                {
                    int itemID = int.Parse(id.ToString());
                    int itemCount = int.Parse(count.ToString());

                    int result = await DBManager.Instance.Create_Item(uid, int.Parse(id.ToString()), int.Parse(count.ToString()));
                    if (result > 0)
                    {
                        Startup._SendToClient(uid, JsonSerializer.Serialize(new { success = "true" }, Startup.jsonOptions)); // 일단 아이템 생성

                        Console.WriteLine($"[WebSocket] 일단 아이템 생성 성공 : {itemID}, {result}");
                        string reason = "서버에 의한 아이템 생성";
                        try
                        {
                            string txHash = await BlockChainManager.Instance.MintItem(uid, itemID, result, itemCount, reason);
                            if (string.IsNullOrEmpty(txHash))
                            {
                                await DBManager.Instance.Delete_Item(uid, itemID, result, itemCount);
                                Console.WriteLine($"[WebSocket] 블록체인 민팅 실패, 아이템 삭제: {itemID}, {result}");
                                return JsonSerializer.Serialize(new { success = "false", error = "Minting failed" }, Startup.jsonOptions);
                            }
                            Console.WriteLine($"[WebSocket] 블록체인 민팅 성공: {itemID}, {result}");
                            return JsonSerializer.Serialize(new { success = "true" }, Startup.jsonOptions); // 사용 가능으로 호출

                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[WebSocket] 아이템 생성 중 오류 발생 : {ex.Message}");
                            await DBManager.Instance.Delete_Item(uid, itemID, result, itemCount);
                            return JsonSerializer.Serialize(new { success = "false" }, Startup.jsonOptions);

                        }
                    }
                    else
                    {
                        Console.WriteLine("[WebSocket] 아이템 생성 실패");
                        return JsonSerializer.Serialize(new { success = "false" }, Startup.jsonOptions);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WebSocket] 아이템 생성 중 오류 발생 : {ex.Message}");
                return JsonSerializer.Serialize(new { success = "false" }, Startup.jsonOptions);
            }
            return JsonSerializer.Serialize(new { success = "false" }, Startup.jsonOptions);
        }
        //public static async Task<string> trade_requestHandler(int uid, JsonElement data)
        //{

        //}

        //public static async Task<string> item_modifyHandler(int uid, JsonElement data)
        //{

        //}

        public static async Task<string> trade_requestHandler(int uid, JsonElement data)
        {
            try
            {

                //역직렬화가 계속 안되는 문제 발생 그냥 TryGetProperty로 처리

                if (data.TryGetProperty("receiverId", out JsonElement receiverId) && data.TryGetProperty("items", out JsonElement items))
                {
                    int receiver = int.Parse(receiverId.ToString());
                    Console.WriteLine($"[WebSocket] 거래 요청 : {uid} -> {receiver}");

                //    //분리하려고 했으나 static이라서 일단 보류
                //    List<TradeItem> tradeItems = new List<TradeItem>();

                //    foreach (JsonElement item in items.EnumerateArray())
                //    {
                //        if (item.TryGetProperty("itemId", out JsonElement itemIdElement) &&
                //            item.TryGetProperty("uniqueId", out JsonElement uniqueIdElement) &&
                //            item.TryGetProperty("count", out JsonElement countElement))
                //        {
                //            int tokenId = await DBManager.Instance.GetTokenId(itemIdElement.GetInt32(), uniqueIdElement.GetInt32()) ?? -1;
                //            if(tokenId != -1)
                //            tradeItems.Add(new TradeItem(itemIdElement.GetInt32(), uniqueIdElement.GetInt32(), countElement.GetInt32(), tokenId));
                //        }
                //        else
                //        {
                //            Console.WriteLine("[ERROR] JSON에서 일부 아이템 정보가 누락됨!");
                //            return JsonSerializer.Serialize(new { success = "false", error = "Invalid item format" }, Startup.jsonOptions);
                //        }
                //    }
                //    bool result = await DBManager.Instance.Trade_Item(uid, receiver, tradeItems);
                //    if (result)
                //    {
                //        Startup._SendToClient(int.Parse(receiverId.ToString()), JsonSerializer.Serialize(new { trade_request = true }, Startup.jsonOptions));
                //        return JsonSerializer.Serialize(new { success = "true" }, Startup.jsonOptions);
                //    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WebSocket] 거래 요청 처리 중 오류 발생 : {ex.Message}");
                return JsonSerializer.Serialize(new { success = "false" }, Startup.jsonOptions);
            }
            return JsonSerializer.Serialize(new { success = "false" }, Startup.jsonOptions);
        }

        public static async Task<string> trade_createHandler(int uid, JsonElement data)
        {
            try
            {
                if (data.TryGetProperty("receiverId", out JsonElement receiverId) && data.TryGetProperty("senderId", out JsonElement senderId))
                {
                    int receiver = int.Parse(receiverId.ToString());
                    int sender = int.Parse(senderId.ToString());
                    Console.WriteLine($"[WebSocket] 거래 요청 : {senderId} -> {receiver}");
                    if (TradeManager.Instance.AddTrade(sender, receiver))
                    {
                        string s = JsonSerializer.Serialize(new
                        {
                            trade_create = new
                            {
                                senderId = sender,
                                receiverId = receiver
                            }
                        }, Startup.jsonOptions);

                        await Startup._SendToClient(receiver, s);
                        return "";
                    }
                    else
                    {
                        Console.WriteLine("[WebSocket] 거래 생성 실패");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WebSocket] 거래 요청 처리 중 오류 발생 : {ex.Message}");
                return JsonSerializer.Serialize(new { success = "false" }, Startup.jsonOptions);
            }
            return JsonSerializer.Serialize(new { success = "false" }, Startup.jsonOptions);
        }

        public static async Task<string> trade_AcceptHandler(int uid, JsonElement data)
        {
            try
            {
                if (data.TryGetProperty("receiverId", out JsonElement receiverId) && data.TryGetProperty("senderId", out JsonElement senderId) && data.TryGetProperty("isAccept", out JsonElement isAccept))
                {
                    int receiver = int.Parse(receiverId.ToString());
                    int sender = int.Parse(senderId.ToString());
                    bool accept = bool.Parse(isAccept.ToString());
                    Console.WriteLine($"[WebSocket] 거래 : {senderId} -> {receiver} : {accept}  실행 : {uid}");

                    bool isTradeAccept = TradeManager.Instance.AcceptTrade(sender, receiver, accept, uid);

                    string senderAddress = (await DBManager.Instance.GetAddressByUID(sender)).pr_Address;
                    string receiverAddress = (await DBManager.Instance.GetAddressByUID(receiver)).pr_Address;
                    string contractAddress = BlockChainManager.Instance.GetContractAddress();
                    int chainId = BlockChainManager.Instance.GetChainId();

                    string s = JsonSerializer.Serialize(new
                    {
                        trade_Accept_response = new
                        {
                            senderId = sender,
                            receiverId = receiver,
                            isSuccess = isTradeAccept,
                            sender = senderAddress,
                            receiver = receiverAddress,
                            _ContractAddress = contractAddress,
                            ChainID = chainId
                        }
                    }, Startup.jsonOptions);
                    if (uid == receiver)
                        await Startup._SendToClient(sender, s);
                    return s;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WebSocket] 거래 요청 처리 중 오류 발생 : {ex.Message}");
                return JsonSerializer.Serialize(new { success = "false" }, Startup.jsonOptions);
            }
            return JsonSerializer.Serialize(new { success = "false" }, Startup.jsonOptions);
        }
        public static async Task<string> Trade_UpdateHandler(int uid, JsonElement data)
        {
            try
            {
                if (data.TryGetProperty("id", out JsonElement Id) && data.TryGetProperty("uniqueId", out JsonElement uniqueId) &&
                    data.TryGetProperty("count", out JsonElement count) && data.TryGetProperty("isIncrease", out JsonElement isIncrease) &&
                    data.TryGetProperty("receiverId", out JsonElement receiverId) && data.TryGetProperty("senderId", out JsonElement senderId))
                {
                    int receiver = int.Parse(receiverId.ToString());
                    int sender = int.Parse(senderId.ToString());
                    int ID = int.Parse(Id.ToString());
                    int UniqueID = int.Parse(uniqueId.ToString());
                    int Count = int.Parse(count.ToString());
                    bool IsIncrease = bool.Parse(isIncrease.ToString());
                    int tokenId = await DBManager.Instance.GetTokenId(ID, UniqueID) ?? -1;

                    if(tokenId != -1)
                    {
                        TradeItem item = new TradeItem(ID, UniqueID, Count,tokenId);
                        bool isSend = TradeManager.Instance.Cal_Data(sender, receiver, uid, item, IsIncrease);

                        if (isSend)
                        {
                            UserItem i = await DBManager.Instance.GetItem(ID, UniqueID);
                            Console.WriteLine($"데이터 값 : id {i.Id} uid : {i.Uid} UniqueID : {i.UniqueId}");
                            if (i != null)
                            {
                                string json = JsonSerializer.Serialize(new
                                {
                                    trade_item_update_response = new
                                    {
                                        senderId = sender,
                                        receiverId = receiver,
                                        userItem = new
                                        {
                                            id = i.Id,
                                            uid = i.Uid,
                                            uniqueId = i.UniqueId,
                                            name = i.Name,
                                            description = i.Description,
                                            state = i.State,
                                            type = i.Type,
                                            count = i.Count,
                                            etc = i.Etc,
                                            image = i.Image
                                        },
                                        isIncrease = IsIncrease
                                    }
                                }, Startup.jsonOptions);
                                if (uid == sender)
                                    await Startup._SendToClient(receiver, json);
                                else
                                    await Startup._SendToClient(sender, json);
                                return "";
                            }
                            else
                            {
                                Console.WriteLine($"[WebSocket] 거래 데이터 전송 실패 (아이템 확인 실패)");
                                return "";
                            }
                        }
                        else
                        {
                            Console.WriteLine($"[WebSocket] 거래 데이터 전송 실패");
                            return "";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WebSocket] 거래 요청 처리 중 오류 발생 : {ex.Message}");
                return JsonSerializer.Serialize(new { success = "false" }, Startup.jsonOptions);
            }
            return JsonSerializer.Serialize(new { success = "false" }, Startup.jsonOptions);
        }

        public static async Task<string> Trade_CallPrograssHandler(int uid, JsonElement data)
        {
            try
            {
                if (
                    data.TryGetProperty("isProgress", out JsonElement Progress) &&
                    data.TryGetProperty("receiverId", out JsonElement receiverId) && data.TryGetProperty("senderId", out JsonElement senderId) && data.TryGetProperty("sign", out JsonElement _sign))
                {
                    int receiver = int.Parse(receiverId.ToString());
                    int sender = int.Parse(senderId.ToString());
                    bool isProgress = bool.Parse(Progress.ToString());
                    string sign = _sign.ToString();
 
                    Console.WriteLine($"[WebSocket] 받은 서명 : {sign}");
                    Console.WriteLine($"[WebSocket] 받은 서명 (hex값) : {sign.HexToByteArray().ToHex()}");
                    Console.WriteLine($"[WebSocket] 입력 받은 값 {isProgress}");
                    var (isSuccess, isCompleted) = await TradeManager.Instance.TradeConfirm(sender, receiver, uid, isProgress, sign);
                    Console.WriteLine($"[WebSocket] {isSuccess} {isCompleted}");
                    string json = "";
                    if (isSuccess)
                    {
                        if (isCompleted)
                        {
                            // 거래 완료
                            json = JsonSerializer.Serialize(new
                            {
                                trade_result = new
                                {
                                    senderId = sender,
                                    receiverId = receiver,
                                    isSuccess = true
                                }
                            }, Startup.jsonOptions);
                        }
                        else
                        {
                            //거래 확인 완료 아님
                            Console.WriteLine($"[WebSocket] 거래 데이터 확인");
                            json = JsonSerializer.Serialize(new
                            {
                                trade_confirm = new
                                {
                                    senderId = sender,
                                    receiverId = receiver,
                                    isSuccess = true
                                }
                            }, Startup.jsonOptions);
                        }
                    }
                    else
                    {
                        //거래 실패
                        Console.WriteLine($"[WebSocket] 거래 데이터 실패");
                        json = JsonSerializer.Serialize(new
                        {
                            trade_confirm = new
                            {
                                senderId = sender,
                                receiverId = receiver,
                                isSuccess = false
                            }
                        }, Startup.jsonOptions);
                    }
                    if (uid == sender)
                        await Startup._SendToClient(receiver, json);
                    else
                        await Startup._SendToClient(sender, json);

                    return json;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WebSocket] 거래 요청 처리 중 오류 발생 : {ex.Message}");
                return JsonSerializer.Serialize(new { success = "false" }, Startup.jsonOptions);
            }
            return JsonSerializer.Serialize(new { success = "false" }, Startup.jsonOptions);
        }
        /*
        public static async Task<string> Trade_CallPrograssHandler(int uid, JsonElement data)
        {
            try
            {
                if (
                    data.TryGetProperty("isProgress", out JsonElement Progress) &&
                    data.TryGetProperty("receiverId", out JsonElement receiverId) && data.TryGetProperty("senderId", out JsonElement senderId))
                {
                    int receiver = int.Parse(receiverId.ToString());
                    int sender = int.Parse(senderId.ToString());
                    bool isProgress = bool.Parse(Progress.ToString());

                    Console.WriteLine($"[WebSocket] 입력 받은 값 {isProgress}");
                    var (isSuccess, isCompleted) = await TradeManager.Instance.TradeConfirm(sender, receiver, uid, isProgress);
                    Console.WriteLine($"[WebSocket] {isSuccess} {isCompleted}");
                    string json = "";
                    if (isSuccess)
                    {
                        if (isCompleted)
                        {
                            // 거래 완료
                            json = JsonSerializer.Serialize(new
                            {
                                trade_result = new
                                {
                                    senderId = sender,
                                    receiverId = receiver,
                                    isSuccess = true
                                }
                            }, Startup.jsonOptions);
                        }
                        else
                        {
                            //거래 확인 완료 아님
                            Console.WriteLine($"[WebSocket] 거래 데이터 확인");
                            json = JsonSerializer.Serialize(new
                            {
                                trade_confirm = new
                                {
                                    senderId = sender,
                                    receiverId = receiver,
                                    isSuccess = true
                                }
                            }, Startup.jsonOptions);
                        }
                    }
                    else
                    {
                        //거래 실패
                        Console.WriteLine($"[WebSocket] 거래 데이터 실패");
                        json = JsonSerializer.Serialize(new
                        {
                            trade_confirm = new
                            {
                                senderId = sender,
                                receiverId = receiver,
                                isSuccess = false
                            }
                        }, Startup.jsonOptions);
                    }
                    if (uid == sender)
                        await Startup._SendToClient(receiver, json);
                    else
                        await Startup._SendToClient(sender, json);

                    return json;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WebSocket] 거래 요청 처리 중 오류 발생 : {ex.Message}");
                return JsonSerializer.Serialize(new { success = "false" }, Startup.jsonOptions);
            }
            return JsonSerializer.Serialize(new { success = "false" }, Startup.jsonOptions);
        }*/

        public static async Task<string> Shop_responseHandler(int uid, JsonElement data)
        {
            try
            {
                if (data.TryGetProperty("shopID", out JsonElement ID))
                {
                    int shopID = int.Parse(ID.ToString());

                    var (shop, i) = await DBManager.Instance.GetShopItemList(shopID);

                    Console.WriteLine($"[WebSocket] 상점 ID 값 {shopID} 상점 이름 {shop.name}");
                    string json = "";
                    json = JsonSerializer.Serialize(new
                    {
                        shop_response = new
                        {
                            name = shop.name,
                            data = i.Select(item => new
                            {
                                id = item.Id,
                                name = item.Name,
                                description = item.Description,
                                state = item.State,
                                type = item.Type,
                                etc = item.Etc,
                                image = item.Image,
                            }).ToList()
                        }
                    }, Startup.jsonOptions);

                    Console.WriteLine($"[WebSocket] 데이터 값 {json}");
                    return json;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WebSocket] 상점 요청 처리 중 오류 발생 : {ex.Message}");
                return JsonSerializer.Serialize(new { success = "false" }, Startup.jsonOptions);
            }
            return JsonSerializer.Serialize(new { success = "false" }, Startup.jsonOptions);
        }

        public static async Task<string> Shop_BuyHandler(int uid, JsonElement data)
        {
            try
            {
                if (data.TryGetProperty("id", out JsonElement ID) && data.TryGetProperty("count", out JsonElement Count))
                {
                    int id = int.Parse(ID.ToString());
                    int count = int.Parse(Count.ToString());

                    string json = "";
                    var result = await DBManager.Instance.Create_Item(uid, id, count);
                    Console.WriteLine($"[WebSocket] 상점 {uid} 유저  아이템 구매 : {id}, count {count}");

                    //json = JsonSerializer.Serialize(new
                    //{
                    //    shop_buy_response = new
                    //    {
                    //        id = id,
                    //        count = count,
                    //        isSuccess = result
                    //    }
                    //}, Startup.jsonOptions);

                    if (result > 0)
                    {
                        Startup._SendToClient(uid, JsonSerializer.Serialize(json, Startup.jsonOptions)); // 일단 아이템 생성

                        Console.WriteLine($"[WebSocket] 일단 아이템 생성 성공");
                        string reason = "서버에 의한 아이템 생성";
                        try
                        {
                            string txHash = await BlockChainManager.Instance.MintItem(uid, id, result, count, reason);
                            if (string.IsNullOrEmpty(txHash))
                            {
                                await DBManager.Instance.Delete_Item(uid, id, result, count);
                                Console.WriteLine($"[WebSocket] 블록체인 민팅 실패, 아이템 삭제: {id}, {result}");
                                return JsonSerializer.Serialize(new { success = "false", error = "Minting failed" }, Startup.jsonOptions);
                            }
                            Console.WriteLine($"[WebSocket] 블록체인 민팅 성공: ID : {id}, 고유번호 : {result} Hash : {txHash}");
                            return JsonSerializer.Serialize(new { success = "true" }, Startup.jsonOptions); // 사용 가능으로 호출

                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[WebSocket] 아이템 생성 중 오류 발생 : {ex.Message}");
                            await DBManager.Instance.Delete_Item(uid, id, result, count);
                            return JsonSerializer.Serialize(new { success = "false" }, Startup.jsonOptions);
                        }
                    }
                    else
                    {
                        Console.WriteLine($"[WebSocket] 유저 아이템 구매 실패");
                    }

                    Console.WriteLine($"[WebSocket] 구매 반환 데이터 값 {json}");
                    return json;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WebSocket] 상점 요청 처리 중 오류 발생 : {ex.Message}");
                return JsonSerializer.Serialize(new { success = "false" }, Startup.jsonOptions);
            }
            return JsonSerializer.Serialize(new { success = "false" }, Startup.jsonOptions);
        }

        public static async Task<string> Shop_SellHandler(int uid, JsonElement data)
        {
            try
            {
                if (data.TryGetProperty("id", out JsonElement ID) && data.TryGetProperty("uniqueid", out JsonElement Uniqueid) && data.TryGetProperty("count", out JsonElement Count))
                {
                    int id = int.Parse(ID.ToString());
                    int uniqueID = int.Parse(Uniqueid.ToString());
                    int count = int.Parse(Count.ToString());

                    string json = "";
                    //var isSuccess = await DBManager.Instance.Delete_Item(uid, id, uniqueID, count);
                    Console.WriteLine($"[WebSocket] 상점 {uid} 유저 아이템 판매 : {id}, 고유 아이템 코드 : {uniqueID} count {count}");

                    UserItem item = await DBManager.Instance.GetItem(id, uniqueID);
                    //json = JsonSerializer.Serialize(new
                    //{
                    //    shop_sell_response = new
                    //    {
                    //        id = id,
                    //        uniqueid = uniqueID,
                    //        count = count,
                    //        isSuccess = isSuccess
                    //    }
                    //}, Startup.jsonOptions);
                    if(item != null)
                    {
                        string txHash = await BlockChainManager.Instance.BurnItem(uid, item.TokenId, count);
                        if (string.IsNullOrEmpty(txHash))
                        {
                            Console.WriteLine($"[WebSocket] 블록체인 민팅 실패, 아이템 삭제: {item.TokenId}");
                            //return JsonSerializer.Serialize(new { success = "false", error = "Minting failed" }, Startup.jsonOptions);
                        }
                    }
                    else
                    {
                        Console.WriteLine($"[WebSocket] 해당 아이템 존재하지 않음");
                    }

                        return "";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WebSocket] 상점 요청 처리 중 오류 발생 : {ex.Message}");
                return JsonSerializer.Serialize(new { success = "false" }, Startup.jsonOptions);
            }
            return JsonSerializer.Serialize(new { success = "false" }, Startup.jsonOptions);
        }

        public static async Task<string> AddressLogin_Handler(int uid, JsonElement data)
        {
            try
            {
                if (data.TryGetProperty("address", out JsonElement _address))
                {
                    string address = _address.ToString();

                    string json = "";
                    Console.WriteLine($"[WebSocket] 로그인 요청 {address}");
                    var account = await DBManager.Instance.RegisterAddress(address);

                    if(account.UID > 0)
                    {
                        bool check = await _CheckUserAsync.Invoke(account.UID);
                        if(check)
                        {
                            Console.WriteLine($"[WebSocket] 로그인 데이터 확인 {account.UID} : {address}");
                            json = JsonSerializer.Serialize(new
                            {
                                address_login_response = new
                                {
                                    uid = account.UID
                                }
                            }, Startup.jsonOptions);
                            _AddUser?.Invoke(uid, account.UID);
                        }
                        else
                        {
                            Console.WriteLine($"[WebSocket] 이미 존재하는 로그인 데이터 {account.UID} : {address}");
                            return "";
                        }
                    }
                    else
                    {
                        Console.WriteLine($"[WebSocket] 로그인 실패");
                    }

                    return json;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WebSocket] 로그인 처리 중 오류 발생 : {ex.Message}");
                return JsonSerializer.Serialize(new { success = "false" }, Startup.jsonOptions);
            }
            return JsonSerializer.Serialize(new { success = "false" }, Startup.jsonOptions);
        }

        public async Task<UserItem> FromDTOAsync(UserItemDTO dto, ServerdbContext context)
        {
            var itemInDb = await context.UserItems
                .FirstOrDefaultAsync(i => i.Id == dto.Id && i.UniqueId == dto.UniqueId);

            if (itemInDb == null)
                throw new Exception("해당 아이템을 DB에서 찾을 수 없습니다.");

            return new UserItem
            {
                Id = dto.Id,
                UniqueId = dto.UniqueId,
                Name = dto.Name,
                Description = dto.Description,
                State = dto.State,
                Type = dto.Type,
                Count = dto.Count,
                Uid = dto.Uid,
                Etc = dto.Etc,
                Image = dto.Image,
                TokenId = itemInDb.TokenId,
                Version = itemInDb.Version,
                Reason = itemInDb.Reason,
                Timestamp = itemInDb.Timestamp
            };
        }
        public UserItemDTO ToDTO(UserItem item)
        {
            return new UserItemDTO
            {
                Id = item.Id,
                UniqueId = item.UniqueId,
                Name = item.Name,
                Description = item.Description,
                State = item.State,
                Type = item.Type,
                Count = item.Count,
                Uid = item.Uid,
                Etc = item.Etc,
                Image = item.Image
            };
        }

        public List<UserItemDTO> ToDTOList(List<UserItem> items)
        {
            return items.Select(ToDTO).ToList();
        }

        #region Public_BlockChain

        public static async Task<string> GetTransaction(int uid, JsonElement data) // public 블록체인에 대한 콜백
        {
            try
            {
                if (data.TryGetProperty("type", out JsonElement _type) && data.TryGetProperty("success", out JsonElement _success) &&
                    data.TryGetProperty("txHash", out JsonElement _txHash))
                {
                    FuncType type = (FuncType)Enum.Parse(typeof(FuncType), _type.ToString());
                    bool isSuccess = bool.Parse(_success.ToString());
                    string txHash = _txHash.ToString();

                    if(!isSuccess || txHash == null)
                    {
                        if(data.TryGetProperty("error", out JsonElement _error))
                        {
                            string error = _error.ToString();
                            Console.WriteLine($"[WebSocket] 블록체인 트랜잭션 실패 : {type} : {txHash} : {error}");
                        }
                        else
                        {
                            Console.WriteLine($"[WebSocket] 블록체인 트랜잭션 실패 : {type} : {txHash}");
                        }
                        return "";
                    }
                    Console.WriteLine($"[WebSocket] Transaction CallBack {txHash}");

                    switch (type)
                    {
                        case FuncType.BURN:
                            Console.WriteLine($"[WebSocket] 소각 허용 : {txHash}");
                            break;
                        case FuncType.APPROVE:
                            Console.WriteLine($"[WebSocket] 권한 부여 허용 : {txHash}");
                            break;
                        case FuncType.TRADE:
                            Console.WriteLine($"[WebSocket] 교환 허용 : {txHash}");
                            break;
                        case FuncType.UPGRADE:
                            Console.WriteLine($"[WebSocket] 업그레이드 허용 : {txHash}");
                            break;
                        default:
                            Console.WriteLine($"[WebSocket] 알 수 없는 트랜잭션 타입 : {type}");
                            break;
                    }



                    return "";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WebSocket] 로그인 처리 중 오류 발생 : {ex.Message}");
                return JsonSerializer.Serialize(new { success = "false" }, Startup.jsonOptions);
            }
            return JsonSerializer.Serialize(new { success = "false" }, Startup.jsonOptions);
        }
        #endregion

    }
}


public struct TradeItem
{
    [JsonPropertyName("itemId")]
    public int itemId;
    [JsonPropertyName("uniqueId")]
    public int uniqueId;
    [JsonPropertyName("count")]
    public int count;
    public int tokenId;

    public TradeItem(int itemId, int uniqueId, int count, int tokenId)
    {
        this.itemId = itemId;
        this.uniqueId = uniqueId;
        this.count = count;
        this.tokenId = tokenId;
    }
    public bool Check(TradeItem item)
    {
        if (itemId == item.itemId && uniqueId == item.uniqueId) return true;
        return false;
    }
}

public class Trade_Request
{
    [JsonPropertyName("receiverId")]
    public int receiverId;
    [JsonPropertyName("items")]
    public List<TradeItem> items;
}
