using Nethereum.Contracts;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;
using Server.Database;
using System.Collections.Concurrent;
using Nethereum.JsonRpc.WebSocketStreamingClient;
using Nethereum.Contracts.ContractHandlers;
using Nethereum.Web3.Accounts;
using Nethereum.RPC.Eth.Subscriptions;
using Nethereum.ABI.FunctionEncoding.Attributes;
using System.Numerics;
using Nethereum.JsonRpc.WebSocketClient;
using Nethereum.ABI;
using Nethereum.Signer;
using Nethereum.Util;
using System.Text;
using Nethereum.Model;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Contracts.Standards.ERC20.TokenList;
using Server.Web;
using System.Text.Json;
using System.Xml;
using Server.Trade;
using System.Diagnostics;
using Nethereum.Contracts.Standards.ERC1155.ContractDefinition;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI.Model;
using Nethereum.Hex.HexTypes;

namespace Server.BlockChain
{
    public enum FuncType
    {
        MINT,
        BURN,
        TRADE,
        UPGRADE,
        APPROVE
    }

    public interface IBlockCain
    {
        Task<string> MintItem(int uid, int itemId, int serial, int amount, string reason);
        Task<string> BurnItem(int uid, int tokenId, int amount);
        //Task<bool> TradeItem(TradeItemsByServerFunction_Sign txData);
        Task<string> UpdateItemVersion(int uid, int tokenId, int serial, int newVersion, string reason);
    }

    public class BlockChainManager : Manager.ManagerInterface, IBlockCain
    {
        public static BlockChainManager Instance { get; } = new BlockChainManager();
        private Web3 _web3;
        private string _contractAddress;
        private ContractHandler _contractHandler;
        private int _chainId;

        private StreamingWebSocketClient _wsClient;
        private EthNewBlockHeadersSubscription _subscription;
        private string _wsUrl;

        private AccountManager _accountManager;

        private readonly ConcurrentDictionary<string, TransactionStatusTracker> _pendingTransactions =
            new ConcurrentDictionary<string, TransactionStatusTracker>();

        private CancellationTokenSource _monitoringCancel;

        private readonly BigInteger _defaultGasPrice = 0;
        private readonly BigInteger _defaultGasLimit = 8000000;
        private readonly int _maxTransactionRetries = 3;
        private readonly int _transactionTimeoutSeconds = 120;
        private System.Timers.Timer _transactionMonitorTimer;

        private PublicBlockChain _publicBlockChain;

        public void Init()
        {
            _accountManager = new AccountManager();
            _wsUrl = Environment.GetEnvironmentVariable("PRIVATE_URL");
            _contractAddress = Environment.GetEnvironmentVariable("PRIVATE_CONTRACT_ADDRESS"); // deploy된 컨트랙트 주소
            _chainId = int.Parse(Environment.GetEnvironmentVariable("PRIVATE_CHAIN_ID")); 
            TimerInit();
            InitWebSocket(_accountManager._serverAccount_private).GetAwaiter().GetResult();
            _publicBlockChain = new PublicBlockChain(_accountManager);
        }

        public string GetContractAddress()
        {
            return _contractAddress;
        }

        public int GetChainId()
        {
            return _chainId;
        }

        private void TimerInit()
        {
            _transactionMonitorTimer = new System.Timers.Timer(1000); // 10초마다 체크
            _transactionMonitorTimer.Elapsed += MonitorPendingTransactions;
            _transactionMonitorTimer.AutoReset = true;
            _transactionMonitorTimer.Start();
        }

        public string CreatedBlockChain(int uid)
        {
            Console.WriteLine($"[BlockChain_p] 프라이빗 블록체인 계정 생성 {uid}");
            return _accountManager.LoadAccount(AccountType.User,uid).Address;
        }

        private async Task InitWebSocket(Nethereum.Web3.Accounts.Account server)
        {
            try
            {
                Console.WriteLine($"[BlockChain_p] WebSocket 연결 시작: {_wsUrl}");
                _wsClient = new StreamingWebSocketClient(_wsUrl);
                var wsClient = new Nethereum.JsonRpc.WebSocketClient.WebSocketClient(_wsUrl);
                await _wsClient.StartAsync();
                _web3 = new Web3(server, wsClient);
                _contractHandler = _web3.Eth.GetContractHandler(_contractAddress);

                await TestConnection();
                await StartEventMoniter();
                Console.WriteLine("[BlockChain_p] WebSocket 연결 완료");
            }
            catch (Exception e)
            {
                Console.WriteLine($"[BlockChain_p] WebSocket 연결 실패: {e.Message}");
            }
        }

        private async Task TestConnection()
        {
            try
            {
                var blockNumber = await _web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
                Console.WriteLine($"[BlockChain_p] 연결 테스트 성공 - 현재 블록: {blockNumber.Value}");
                var chainId = await _web3.Eth.ChainId.SendRequestAsync();
                if (chainId.Value != _chainId)
                {
                    Console.WriteLine($"[BlockChain_p] 연결된 블록체인 ID 오류 : {chainId.Value} 해당 값으로 변경");
                    _chainId = (int)chainId.Value;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BlockChain_p] 연결 테스트 실패: {ex.Message}");
                throw;
            }
        }

        private async Task StartEventMoniter()
        {
            try
            {
                if (_subscription == null)
                {
                    _monitoringCancel = new CancellationTokenSource();
                    _subscription = new EthNewBlockHeadersSubscription(_wsClient);
                    //TODO : 이벤트 발생시 유저에게 알림
                    _subscription.SubscriptionDataResponse += async (sender, blockHeader) =>
                    {
                        try
                        {
                            var logs = await _web3.Eth.Filters.GetLogs.SendRequestAsync(new NewFilterInput
                            {
                                FromBlock = new BlockParameter(blockHeader.Response.Number),
                                ToBlock = new BlockParameter(blockHeader.Response.Number),
                                Address = new[] { _contractAddress }
                            });

                            foreach (var log in logs)
                            {
                                await ProcessLog(log);
                            }
                            await CheckPendingTransaction(blockHeader.Response.Number);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[BlockChain_p] 이벤트 수신 중 오류: {ex.Message}");
                        }
                    };
                    await _subscription.SubscribeAsync();
                    Console.WriteLine($"[BlockChain_p] 이벤트 구독 완료");
                }
                else
                {
                    Console.WriteLine("[BlockChain_p] 블록 헤더 이벤트 구독이 이미 되어 있습니다.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BlockChain_p] 이벤트 구독 중 오류: {ex.Message}");
                throw;
            }
        }

        private async Task ProcessLog(FilterLog log)
        {
            try
            {
                _pendingTransactions.TryGetValue(log.TransactionHash, out var tracker);
                if (tracker == null)
                {
                    Console.WriteLine($"[BlockChain_p] 트랜잭션 해시를 찾을 수 없습니다: {log.TransactionHash}");
                    return;
                }

                var mintEvent = Event<ItemMintDTO>.DecodeEvent(log);
                if (mintEvent != null)
                {
                    int tokenId = (int)mintEvent.Event.tokenId;
                    int itemId = (int)mintEvent.Event.itemId;
                    int serial = (int)mintEvent.Event.serial;

                    Console.WriteLine($"[BlockChain] NFT 민팅 완료 : TokenId={mintEvent.Event.tokenId}, ItemId={mintEvent.Event.itemId}, serial={mintEvent.Event.serial}");

                    try
                    {
                        var item = await DBManager.Instance.GetItem(itemId, serial);
                        if (item != null)
                        {
                            await DBManager.Instance.UpdateItemToken(itemId, serial, tokenId, 1, tracker.data.reason,tracker.data.timestamp);

                            Console.WriteLine($"[BlockChain_p] NFT 사용가능 및 세부 정보 변경 완료");
                            Console.WriteLine($"[BlockChain_public] NFT Public 발행 진행");
                            await _publicBlockChain.Request(tracker.data, tracker.func as MintitemFunction_sign);
                            //TODO : 클라이언트에게 성공 진행
                        }
                        else
                        {
                            Console.WriteLine($"[BlockChain_p] NFT 민팅은 성공했으나 DB추가 실패");
                            //TODO : 클라이언트에게 실패 진행
                        }
                        return;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[BlockChain_p] NFT 민팅 처리 중 오류: {ex.Message}");
                    }
                }

                var burnEvent = Event<ItemBurnDTO>.DecodeEvent(log);
                if (burnEvent != null)
                {
                    int itemId = (int)burnEvent.Event.itemId;
                    int serial = (int)burnEvent.Event.serial;

                    Console.WriteLine($"[BlockChain_Private] NFT 소각 완료 : TokenId={burnEvent.Event.tokenId}");
                    Console.WriteLine($"[BlockChain_public] NFT 제거 진행");
                    if(tracker.data != null && tracker.func != null)
                    {
                        await _publicBlockChain.Request(tracker.data, tracker.func as ItemBurnFunction);
                    }
                    try
                    {
                        //string json = JsonSerializer.Serialize(new
                        //{
                        //    shop_sell_response = new
                        //    {
                        //        id = itemId,
                        //        uniqueid = serial,
                        //        count = 1,
                        //        isSuccess = true
                        //    }
                        //}, Startup.jsonOptions);
                        //await DBManager.Instance.Delete_Item(itemId, serial, -1);
                        Console.WriteLine($"[BlockChain_p] DB 정리 완료 ID : {itemId} serial : {serial}");
                        //TODO : 클라이언트에게 성공 진행
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[BlockChain_p] NFT 소각 처리 중 오류: {ex.Message}");
                    }

                    return;
                }

                //var tradeEvent = Event<ItemTransferredDTO>.DecodeEvent(log);
                //if (tradeEvent != null)
                //{
                //    var from = tradeEvent.Event.from;
                //    var to = tradeEvent.Event.to;
                //    var tokenId = tradeEvent.Event.ids;
                //    var amount = tradeEvent.Event.amounts;

                //    Console.WriteLine($"[BlockChain] NFT 교환 완료: From={from}, To={to}");
                //    await _publicBlockChain.Request(tracker.data, tracker.func);
                //    await TradeManager.Instance.ChainComplete(from,to);
                //    for (int i = 0; i < tokenId.Count; i++)
                //    {
                //        Console.WriteLine($"TokenId={tokenId[i]}, Amount={amount[i]}");
                //    }
                //}

                var tradeEvent = Event<TradeSuccessEventDTO>.DecodeEvent(log);
                if (tradeEvent != null)
                {
                    var from = tradeEvent.Event.User1;
                    var to = tradeEvent.Event.User2;

                    Console.WriteLine($"[BlockChain] NFT 교환 완료: From={from}, To={to}");
                    if(await TradeManager.Instance.ChainComplete(from, to))
                    {
                        await _publicBlockChain.Request(tracker.data, tracker.func as TradeItemsByServerFunction);
                    }
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine($"[BlockChain_p] 이벤트 처리 중 오류: {ex.Message}");
            }
        }

        private async Task CheckPendingTransaction(Nethereum.Hex.HexTypes.HexBigInteger blockNumber)
        {
            try
            {
                var block = await _web3.Eth.Blocks.GetBlockWithTransactionsByNumber.SendRequestAsync(new BlockParameter(blockNumber)); // 블럭 번호 정보

                if (block?.Transactions == null || !block.Transactions.Any())
                    return;

                foreach (var tx in block.Transactions) // 블록 트랜잭션 순회
                {
                    if (_pendingTransactions.TryRemove(tx.TransactionHash, out var tracker)) // 해시 확인
                    {
                        var receipt = await _web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(tx.TransactionHash);

                        if (receipt != null)
                        {
                            Console.WriteLine($"[BlockChain_p] 트랜잭션 확인됨: {tx.TransactionHash}, 상태: {(receipt.Status.Value == 1 ? "성공" : "실패")}");

                            if (receipt.Status.Value == 1)
                            {
                                tracker.CompletionSource.TrySetResult(receipt);
                                //if (tracker.data != null) await OnTransaction(receipt, tracker.data);
                            }
                            else
                            {
                                tracker.CompletionSource.TrySetException(new Exception($"[BlockChain_p] 트랜잭션 실패: {tx.TransactionHash}"));
                                //if (tracker.data != null) await OnTransaction(tx.TransactionHash, tracker.data);
                                if (tracker.data != null) await OnTransaction(tracker.data);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BlockChain] 블록 트랜잭션 확인 중 오류: {ex.Message}");
            }
        }

        private async Task OnTransaction(TransactionReceipt receipt, TransactionMetadata data) // 트랜잭션 완료시 처리 -> processLog로 대체
        {

        }

        private async Task OnTransaction(TransactionMetadata data) // 트랜잭션 실패시 처리
        {
            if (data.status == FuncType.MINT)
            {
                await DBManager.Instance.Delete_Item(data.uid, data.itemId, data.uniqueId, data.count);
                Console.WriteLine($"[BlockChain_p] 민팅 실패 : 아이템={data.itemId}, UniqueId={data.uniqueId}");
                //TODO : 클라이언트 실패 전송(아이템 삭제)
            }
            else if (data.status == FuncType.BURN)
            {
                Console.WriteLine($"[BlockChain_p] Brun 실패 : 아이템={data.itemId}, UniqueId={data.uniqueId}");
            }
            else if (data.status == FuncType.TRADE)
            {
                Console.WriteLine($"[BlockChain_p] 교환 실패");
            }
            else if (data.status == FuncType.UPGRADE)
            {
                Console.WriteLine($"[BlockChain_p] 업그레이드 실패");
            }
        }



        private void MonitorPendingTransactions(object sender, System.Timers.ElapsedEventArgs e)
        {
            var expiredTxs = _pendingTransactions
        .Where(pair => (DateTime.UtcNow - pair.Value.StartTime).TotalSeconds > _transactionTimeoutSeconds)
        .ToList();

            foreach (var tx in expiredTxs)
            {
                Console.WriteLine($"[BlockChain_p] 트랜잭션 타임아웃: {tx.Key}");
                tx.Value.CompletionSource.TrySetException(
                    new TimeoutException($"Transaction timeout: {tx.Key}")
                );
                _pendingTransactions.TryRemove(tx.Key, out _);
            }
        }

        public async Task<string> MintItem(int uid, int itemId, int serial, int amount, string reason)
        {
            try
            {
                Console.WriteLine($"[BlockChain_p] NFT 발행 시작");
                var account = _accountManager.LoadAccount(AccountType.User, uid);
                var web3 = new Web3(_accountManager._serverAccount_private, _web3.Client);
                var handler = web3.Eth.GetContractHandler(_contractAddress);
                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var dataHash = GetItemDataHash(itemId, serial, reason, timestamp);
                var signature = GetSign(dataHash);


                Console.WriteLine($"[BlockChain_p] NFT 발급 주소 UID : {uid}, 주소 : {account.Address}");
                var tx = new MintitemFunction_sign
                {
                    to = account.Address,
                    itemId = itemId,
                    serial = serial,
                    amount = amount,
                    dataHash = dataHash,
                    signature = signature
                };

                var txHash = await handler.SendRequestAsync(tx);
                Console.WriteLine($"[BlockChain_p] NFT 발행 요청: TxHash={txHash}");

                var receiptTcs = new TaskCompletionSource<TransactionReceipt>();
                var tracker = new TransactionStatusTracker(receiptTcs);
                tracker.data = new TransactionMetadata
                {
                    itemId = itemId,
                    uniqueId = serial,
                    count = amount,
                    uid = uid,
                    status = FuncType.MINT,
                    version = 1,
                    reason = reason,
                    timestamp = timestamp
                };
                tracker.func = tx;

                _pendingTransactions[txHash] = tracker;
                return txHash;
            }
            catch(Exception ex)
            {
                Console.WriteLine($"[BlockChain_p] NFT 발행 실패 {ex}");
                return "";
            }
            
        }
        public async Task<string> BurnItem(int uid, int tokenId, int amount)
        {
            Console.WriteLine($"[BlockChain_p] NFT Burn 시작");
            var account = _accountManager.LoadAccount(AccountType.User, uid);
            var web3 = new Web3(_accountManager._serverAccount_private, _web3.Client);
            var handler = web3.Eth.GetContractHandler(_contractAddress);
            var receiptTcs = new TaskCompletionSource<TransactionReceipt>();
            var tracker = new TransactionStatusTracker(receiptTcs);

            var tx = new ItemBurnFunction
            {
                from = account.Address,
                tokenId = tokenId,
                amount = amount,
            };
            var txHash = await handler.SendRequestAsync(tx);
            
            Console.WriteLine($"[BlockChain_p] NFT Burn 요청: TxHash={txHash}");
            tracker.data = new TransactionMetadata
            {
                count = amount,
                uid = uid,
                status = FuncType.BURN,
            };
            tracker.func = tx;

            _pendingTransactions[txHash] = tracker;
            return txHash;
        }

        public async Task<Dictionary<int, BigInteger>> GetUserOwnedItems(string userAddress, List<int> allTokenIds)
        {
            var web3 = new Web3(_web3.Client);
            var queryHandler = web3.Eth.GetContractQueryHandler<BalanceOfFunction>();

            var result = new Dictionary<int, BigInteger>();

            foreach (var tokenId in allTokenIds)
            {
                var balance = await queryHandler
                    .QueryAsync<BigInteger>(_contractAddress, new BalanceOfFunction
                    {
                        Owner = userAddress,
                        TokenId = tokenId
                    });

                if (balance > 0)
                {
                    result[tokenId] = balance;
                }
            }

            return result;
        }

        //public async Task<bool> TradeItem(TradeItemsByServerFunction_Sign txData, string user1Signer, string user2Signer)
        //{
        //    //var fromAccount = _accountManager.LoadAccount(AccountType.User, fromUid);
        //    //var toAccount = _accountManager.LoadAccount(AccountType.User, toUid);
        //    var web3 = new Web3(_accountManager.LoadAccount(AccountType.Server_Private), _web3.Client); // 서버 계정으로 실행
        //    var handler = web3.Eth.GetContractHandler(_contractAddress);

        //    try
        //    {
        //        //if (TestVerify(txData.User1, txData.User2, txData.User1TokenIds, txData.User1Amounts, txData.User2TokenIds, txData.User2Amounts, _contractAddress, _chainId, txData.User1Sig, txData.User2Sig, user1Signer, user2Signer))
        //        {
        //            Console.WriteLine($"[BlockChain_p] 데이터 서명 문제 없음");
        //            Console.WriteLine($"[BlockChain_p] NFT 교환 시작");
        //            var txHash = await handler.SendRequestAsync(txData);

        //            Console.WriteLine($"[BlockChain_p] NFT 교환 요청 From{txData.User1} -> TO {txData.User2} : TxHash={txHash}");

        //            var receiptTcs = new TaskCompletionSource<TransactionReceipt>();
        //            var tracker = new TransactionStatusTracker(receiptTcs);
        //            _pendingTransactions[txHash] = tracker;
        //            Console.WriteLine($"[BlockChain_p] NFT 교환 Hash = {txHash}");
        //            tracker.data = new TransactionMetadata
        //            {
        //                sender = txData.TradeInfo.User1,
        //                receiver = txData.TradeInfo.User2,
        //                status = FuncType.TRADE,
        //            };
        //            tracker.func = txData;
        //            return true;
        //        }
        //        return false;
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($"[BlockChain_p] NFT 교환 실패 {ex}");
        //        return false;
        //    }

        //    return false;
        //}

        public async Task<bool> TradeItemRawABI_Compatible(TradeItemsByServerFunction_Sign txData)
        {
            try
            {
                var serverAccount = _accountManager.LoadAccount(AccountType.Server_Private);
                var web3 = new Web3(serverAccount, _web3.Client);

                var handler = web3.Eth.GetContractTransactionHandler<TradeItemsByServerFunction_Sign>();

                // 트랜잭션 인풋 생성 (gas 자동 추정)
                var txInput = await handler.CreateTransactionInputEstimatingGasAsync(_contractAddress, txData);

                // 트랜잭션 전송
                var txHash = await web3.Eth.Transactions.SendTransaction.SendRequestAsync(txInput);
                Console.WriteLine($"[BlockChain_p] RawABI NFT 교환 TxHash = {txHash}");

                // 트랜잭션 추적기 등록
                var tracker = new TransactionStatusTracker(new TaskCompletionSource<TransactionReceipt>())
                {
                    data = new TransactionMetadata
                    {
                        sender = txData.TradeInfo.User1,
                        receiver = txData.TradeInfo.User2,
                        status = FuncType.TRADE
                    }
                };
                _pendingTransactions[txHash] = tracker;

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BlockChain_p] RawABI NFT 교환 실패: {ex}");
                return false;
            }
        }

        // 서버 측 코드 - 서명 검증 함수
        public bool VerifySignature(byte[] signature, string expectedAddress, string tradeHashHex)
        {
            Console.WriteLine($"[DEBUG] 검증 시작: 해시={tradeHashHex}, 예상주소={expectedAddress}");

            // 1. tradeHashHex → byte[32]
            byte[] hashBytes = tradeHashHex.Replace("0x", "").HexToByteArray();
            if (hashBytes.Length != 32)
            {
                Console.WriteLine($"[ERROR] 해시 길이가 {hashBytes.Length}바이트입니다. 32바이트가 필요합니다.");
                return false;
            }

            // 2. prefix 붙이기 (encode_defunct 방식)
            byte[] prefix = Encoding.ASCII.GetBytes("\x19Ethereum Signed Message:\n32");
            byte[] fullMessage = prefix.Concat(hashBytes).ToArray();

            // 3. 해시 계산
            byte[] finalHash = Sha3Keccack.Current.CalculateHash(fullMessage);

            // 4. 서명 복구
            var signer = new EthereumMessageSigner();
            string recoveredAddress = signer.EcRecover(finalHash, signature.ToHex());

            Console.WriteLine($"[DEBUG] 복구된 주소: {recoveredAddress}");
            return recoveredAddress.Equals(expectedAddress, StringComparison.OrdinalIgnoreCase);
        }



        // 메인 검증 함수
        public bool TestVerify(
            string user1, string user2,
            List<BigInteger> user1TokenIds, List<BigInteger> user1Amounts,
            List<BigInteger> user2TokenIds, List<BigInteger> user2Amounts,
            string contractAddress, BigInteger chainId,
            byte[] user1Signature, byte[] user2Signature, string user1Signer, string user2Signer)
        {
            Console.WriteLine("===== 거래 서명 검증 상세 로그 =====");

            // 1. 트레이드 해시 생성
            var tradeHash = GetTradeDataHash(user1, user2, user1TokenIds, user1Amounts,
                                            user2TokenIds, user2Amounts, contractAddress, chainId);
            string tradeHashHex = "0x" + tradeHash.ToHex();
            Console.WriteLine($"[DEBUG] 트레이드 해시: {tradeHashHex}");

            // 2. 서명 검증 - 각 사용자별로 별도 진행
            Console.WriteLine("\n[DEBUG] USER1 서명 검증 시작 -----------------");
            bool valid1 = VerifySignature(user1Signature, user1Signer, tradeHashHex);

            Console.WriteLine("\n[DEBUG] USER2 서명 검증 시작 -----------------");
            bool valid2 = VerifySignature(user2Signature, user2Signer, tradeHashHex);

            // 3. 최종 결과 확인
            Console.WriteLine($"\n[DEBUG] 최종 결과: USER1={valid1}, USER2={valid2}");
            Console.WriteLine("=========================================");

            return valid1 && valid2;
        }

        // MetaMask의 personal_sign과 동일한 방식으로 처리
        public byte[] GetPersonalSignHash(string hexMessage)
        {
            // MetaMask는 personal_sign에 전달된 문자열을 UTF-8 바이트로 변환
            byte[] messageBytes = Encoding.UTF8.GetBytes(hexMessage);

            // 문자열 길이에 맞는 프리픽스 추가 (여기서는 "0x..."의 길이)
            byte[] prefix = Encoding.UTF8.GetBytes($"\x19Ethereum Signed Message:\n{messageBytes.Length}");

            // 프리픽스와 메시지 바이트 연결 후 해시
            byte[] prefixedMessage = prefix.Concat(messageBytes).ToArray();
            return Sha3Keccack.Current.CalculateHash(prefixedMessage);
        }

        //public async Task<bool> TradeItem(int fromUid, int toUid, List<int> tokenIds, List<int> amounts)
        //{
        //    var fromAccount = _accountManager.LoadAccount(AccountType.User, fromUid);
        //    var toAccount = _accountManager.LoadAccount(AccountType.User, toUid);
        //    var web3 = new Web3(fromAccount, _web3.Client);
        //    var handler = web3.Eth.GetContractHandler(_contractAddress);

        //    var balanceQueryHandler = web3.Eth.GetContractQueryHandler<BalanceOfFunction>();
        //    for (int i = 0; i < tokenIds.Count; i++)
        //    {
        //        var balance = await balanceQueryHandler
        //            .QueryAsync<BigInteger>(_contractAddress, new BalanceOfFunction
        //            {
        //                Owner = fromAccount.Address,
        //                TokenId = tokenIds[i]
        //            });

        //        if (balance < amounts[i])
        //        {
        //            Console.WriteLine($"[BlockChain_p] 잔액 부족: TokenId={tokenIds[i]}, 보유={balance}, 필요={amounts[i]}");
        //            return false;
        //        }
        //        else if(balance == 0)
        //        {
        //            Console.WriteLine($"[BlockChain_p] 현재 보유 개수가 0개");
        //            return false;
        //        }
        //    }
        //    for (int i = 0; i < tokenIds.Count; i++)
        //    {
        //        var tokenId = tokenIds[i];
        //        var balance = await balanceQueryHandler.QueryAsync<BigInteger>(_contractAddress, new BalanceOfFunction
        //        {
        //            Owner = fromAccount.Address,
        //            TokenId = tokenId
        //        });

        //        Console.WriteLine($"[BlockChain_p] 거래 전 확인: UID={fromUid}, TokenId={tokenId}, 보유 수량={balance}, 전송 수량={amounts[i]}");
        //    }


        //    var tx = new SafeBatchTransferFromFunction
        //    {
        //        from = fromAccount.Address,
        //        to = toAccount.Address,
        //        ids = tokenIds,
        //        amounts = amounts,
        //        data = new byte[0]
        //    };

        //    try
        //    {
        //        var txHash = await handler.SendRequestAsync(tx);

        //        Console.WriteLine($"[BlockChain_p] NFT 교환 요청 From{fromUid} -> TO {toUid} : TxHash={txHash}");

        //        var receiptTcs = new TaskCompletionSource<TransactionReceipt>();
        //        var tracker = new TransactionStatusTracker(receiptTcs);
        //        _pendingTransactions[txHash] = tracker;
        //        Console.WriteLine($"[BlockChain_p] NFT 교환 Hash = {txHash}");
        //        return true;
        //    }
        //    catch(Exception ex)
        //    {
        //        Console.WriteLine($"[BlockChain_p] NFT 교환 실패 {ex}");
        //        return false;
        //    }

        //    return false;
        //}

        //public async Task<string> TradeItem(int fromUid, int toUid, List<int> tokenIds, List<int> amounts)
        //{
        //    var fromAccount = _accountManager.LoadAccount(AccountType.User, fromUid);
        //    var toAccount = _accountManager.LoadAccount(AccountType.User, toUid);
        //    var web3 = new Web3(fromAccount, _web3.Client);
        //    var handler = web3.Eth.GetContractHandler(_contractAddress);

        //    var tx = new SafeBatchTransferFromFunction
        //    {
        //        from = fromAccount.Address,
        //        to = toAccount.Address,
        //        ids = tokenIds,
        //        amounts = amounts,
        //        data = new byte[0]
        //    };

        //    var txHash = await handler.SendRequestAsync(tx);

        //    Console.WriteLine($"[BlockChain_p] NFT 교환 요청 From{fromUid} -> TO {toUid} : TxHash={txHash}");

        //    var receiptTcs = new TaskCompletionSource<TransactionReceipt>();
        //    var tracker = new TransactionStatusTracker(receiptTcs);
        //    _pendingTransactions[txHash] = tracker;
        //    Console.WriteLine($"[BlockChain_p] NFT 교환 Hash = {txHash}");
        //    return txHash;
        //}

        public async Task<string> UpdateItemVersion(int uid, int tokenId, int serial, int newVersion, string reason)
        {
            var account = _accountManager.LoadAccount(AccountType.User, uid);
            var web3 = new Web3(_accountManager._serverAccount_private, _web3.Client);
            var handler = web3.Eth.GetContractHandler(_contractAddress);

            var timestamp = new BigInteger(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            var newHash = GetItemDataHash(tokenId, serial, reason, timestamp, newVersion);
            var signature = GetSign(newHash);  // 서버의 ECDSA 서명

            var tx = new UpdateItemVersionFunction_sign
            {
                tokenId = tokenId,
                newVersion = newVersion,
                newDataHash = newHash,
                signature = signature,
                reason = reason
            };

            var txHash = await handler.SendRequestAsync(tx);
            Console.WriteLine($"[BlockChain_p] NFT 업데이트 요청: TxHash={txHash}");

            var receiptTcs = new TaskCompletionSource<TransactionReceipt>();
            var tracker = new TransactionStatusTracker(receiptTcs);
            _pendingTransactions[txHash] = tracker;
            return txHash;
        }

        public async Task<bool> VerifyItem(int uid, int tokenId, byte[] signature) // 
        {
            var account = _accountManager.LoadAccount(AccountType.User, uid);
            var web3 = new Web3(account, _web3.Client);
            var handler = web3.Eth.GetContractHandler(_contractAddress);

            var function = new VerifyItemFunction
            {
                tokenId = tokenId,
                signature = signature
            };

            return await handler.QueryAsync<VerifyItemFunction, bool>(function);
        }

        public byte[] GetItemDataHash(int itemId, int serial, string reason, BigInteger timestamp, int version = 1)
        {
            var encoder = new ABIEncode();
            var values = new List<ABIValue>
            {
                new ABIValue("address", _accountManager._serverAccount_private.Address),
                new ABIValue("uint256", _chainId),
                new ABIValue("uint256", itemId),
                new ABIValue("uint256", serial),
            };
            values.Add(new ABIValue("uint256", version));
            values.Add(new ABIValue("string", reason));
            values.Add(new ABIValue("uint256", timestamp));

            var encoded = encoder.GetABIEncoded(values.ToArray());
            return Sha3Keccack.Current.CalculateHash(encoded);
        }

        public byte[] GetTradeDataHash(
            string user1,
            string user2,
            List<BigInteger> user1TokenIds,
            List<BigInteger> user1Amounts,
            List<BigInteger> user2TokenIds,
            List<BigInteger> user2Amounts,
            string contractAddress,
            BigInteger chainId)
        {
            var encoder = new ABIEncode();

            var values = new List<ABIValue>
            {
                new ABIValue("address", contractAddress),
                new ABIValue("uint256", chainId),
                new ABIValue("address", user1),
                new ABIValue("address", user2),
                new ABIValue("uint256[]", user1TokenIds.ToArray()),
                new ABIValue("uint256[]", user1Amounts.ToArray()),
                new ABIValue("uint256[]", user2TokenIds.ToArray()),
                new ABIValue("uint256[]", user2Amounts.ToArray())
            };

            var encoded = encoder.GetABIEncoded(values.ToArray());
            Console.WriteLine("서버 encoded: " + encoded.ToHex(true));
            return Sha3Keccack.Current.CalculateHash(encoded);
        }

        private byte[] GetSign(byte[] dataHash)
        {
            var prefix = Encoding.UTF8.GetBytes("\x19Ethereum Signed Message:\n32");
            var ethMessage = Sha3Keccack.Current.CalculateHash(prefix.Concat(dataHash).ToArray());
            var signatureECDSA = new EthereumMessageSigner().Sign(dataHash, _accountManager._serverAccount_private.PrivateKey);

            var signature = signatureECDSA.HexToByteArray();
            return signature;
        }
    }

    public class TransactionStatusTracker
    {
        public TaskCompletionSource<TransactionReceipt> CompletionSource { get; private set; }
        public DateTime StartTime { get; private set; }
        public TransactionMetadata data { get; set; }
        public FunctionMessage func { get; set; }

        public TransactionStatusTracker(TaskCompletionSource<TransactionReceipt> completionSource)
        {
            CompletionSource = completionSource;
            StartTime = DateTime.UtcNow;
        }
    }
    public class TransactionMetadata
    {
        public string From { get; set; }                // 발신자 주소 
        public string To { get; set; }                  // 컨트랙트 주소
        public string Data { get; set; }                // 인코딩된 함수 호출 데이터
        public int ChainId { get; set; }                // 체인 ID
        public BigInteger GasLimit { get; set; }        // 가스 한도

        public int itemId { get; set; }
        public int uniqueId { get; set; }
        public int count { get; set; }

        public int uid { get; set; }
        public FuncType status { get; set; } // "mint", "burn", "trade" 등
        public int version { get; set; }
        public string reason { get; set; }
        public long timestamp { get; set; }

        public int tokenId { get; set; }
        public string sender { get; set; } // 발신자 주소
        public string receiver { get; set; } // 수신자 주소
        public List<int> sendertokenIds { get; set; }
        public List<int> senderamounts { get; set; }

        public List<int> receivertokenIds { get; set; }
        public List<int> receiveramounts { get; set; }
        public string toAddress { get; set; }
    }

}
