
//using Nethereum.ABI;
//using Nethereum.Contracts;
//using Nethereum.Contracts.ContractHandlers;
//using Nethereum.Contracts.Standards.ERC20.TokenList;
//using Nethereum.Hex.HexConvertors.Extensions;
//using Nethereum.Hex.HexTypes;
//using Nethereum.JsonRpc.WebSocketStreamingClient;
//using Nethereum.RPC.Eth.DTOs;
//using Nethereum.RPC.Eth.Subscriptions;
//using Nethereum.RPC.Net;
//using Nethereum.Signer;
//using Nethereum.Util;
//using Nethereum.Web3;
//using Server.Database;
//using Server.Trade;
//using Server.Web;
//using System.Collections.Concurrent;
//using System.Numerics;
//using System.Text;
//using System.Text.Json;
//using System.Text.Json.Serialization;
//using static System.Runtime.InteropServices.JavaScript.JSType;


//기존 코드 백업 (sign방식과 비교 할때 사용하기 위함)
//namespace Server.BlockChain
//{
//    public class PublicBlockChain
//    {
//        private Web3 _web3Http, _web3Socket;
//        private string _contractAddress;
//        private ContractHandler _contractHandler;
//        private int _chainId;
//        private AccountManager _accountManager;
//        private string _key;
//        private System.Timers.Timer _transactionMonitorTimer;
//        private StreamingWebSocketClient _wsClient;

//        private EthNewBlockHeadersSubscription _subscription;
//        private CancellationTokenSource _monitoringCancel;

//        private readonly int _transactionTimeoutSeconds = 360;
//        private readonly ConcurrentDictionary<string, TransactionStatusTracker> _pendingTransactions =
//    new ConcurrentDictionary<string, TransactionStatusTracker>();
//        private readonly ConcurrentDictionary<int, TransactionMetadata> _pendingMeta = 
//    new ConcurrentDictionary<int, TransactionMetadata>();// 서명 대기 트랜잭션

//        public PublicBlockChain(AccountManager accountManager)
//        {
//            Console.WriteLine("[BlockChain] 퍼블릭 블록체인 초기화");
//            this._accountManager = accountManager;
//            this._contractAddress = Environment.GetEnvironmentVariable("PUBLIC_CONTRACT_ADDRESS"); ;
//            _key = Environment.GetEnvironmentVariable("ALCHEMY_API_KEY");
//            _chainId = int.Parse(Environment.GetEnvironmentVariable("PUBLIC_CHAIN_ID"));
//            TimerInit();
//            InitWebSocket(_accountManager._serverAccount_public).GetAwaiter().GetResult();
//        }

//        public string GetContractAddress()
//        {
//            return _contractAddress;
//        }

//        public int GetChainId()
//        {
//            return _chainId;
//        }
//        private async Task InitWebSocket(Nethereum.Web3.Accounts.Account server)
//        {
//            try
//            {
//                //_wsClient = new StreamingWebSocketClient("wss://eth-sepolia.g.alchemy.com/v2/" + _key);
//                //var wsClient = new Nethereum.JsonRpc.WebSocketClient.WebSocketClient("wss://eth-sepolia.g.alchemy.com/v2/" + _key);

//                _wsClient = new StreamingWebSocketClient("ws://127.0.0.1:9000");
//                var wsClient = new Nethereum.JsonRpc.WebSocketClient.WebSocketClient("ws://127.0.0.1:9000");

//                await _wsClient.StartAsync();
//                _web3Socket = new Web3(server, wsClient);
//                //var httpClient = "https://eth-sepolia.g.alchemy.com/v2/" + _key;
//                var httpClient = "http://127.0.0.1:9000";

//                _web3Http = new Web3(server, httpClient);

//                _contractHandler = _web3Http.Eth.GetContractHandler(_contractAddress);

//                //await ClearStuckNonceIfNeeded();

//                await TestConnection();
//                await StartEventMoniter();

//                Console.WriteLine("[BlockChain_public] WebSocket 연결 완료");
//                await RefreshAllNFTsInCollection();
//            }
//            catch (Exception e)
//            {
//                Console.WriteLine($"[BlockChain_public] WebSocket 연결 실패: {e.Message}");
//            }
//        }

//        private async Task ClearStuckNonceIfNeeded() // 실운영시에만 필요 테스트때는 X
//        {
//            try
//            {
//                var address = _accountManager._serverAccount_public.Address;

//                var latest = await _web3Http.Eth.Transactions.GetTransactionCount.SendRequestAsync(address, BlockParameter.CreateLatest());
//                var pending = await _web3Http.Eth.Transactions.GetTransactionCount.SendRequestAsync(address, BlockParameter.CreatePending());

//                if (pending.Value > latest.Value)
//                {
//                    var stuckNonce = new HexBigInteger(latest.Value);
//                    var currentGasPrice = await _web3Http.Eth.GasPrice.SendRequestAsync();
//                    var minGasPrice = new HexBigInteger(Web3.Convert.ToWei(1, UnitConversion.EthUnit.Gwei));

//                    Console.WriteLine($"[BlockChain_public] Stuck nonce 감지됨. latest={latest.Value}, pending={pending.Value}");

//                    var input = new TransactionInput
//                    {
//                        From = address,
//                        To = address,
//                        Value = new HexBigInteger(0),
//                        Gas = new HexBigInteger(21000),
//                        GasPrice = minGasPrice,
//                        Nonce = stuckNonce
//                    };

//                    var fixTxHash = await _web3Http.Eth.TransactionManager.SendTransactionAsync(input);
//                    Console.WriteLine($"[BlockChain_public] 강제 덮어쓰기 TX 전송됨: {fixTxHash}");

//                    await Task.Delay(3000); // 확산 대기
//                }
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"[BlockChain_public] 덮어쓰기 TX 실패: {ex.Message}");
//            }
//        }

//        private async Task TestConnection()
//        {
//            try
//            {
//                var blockNumber = await _web3Http.Eth.Blocks.GetBlockNumber.SendRequestAsync();
//                Console.WriteLine($"[BlockChain_public] 연결 테스트 성공 - 현재 블록: {blockNumber.Value}");
//                var chainId = await _web3Http.Eth.ChainId.SendRequestAsync();
//                if (chainId.Value != _chainId)
//                {
//                    Console.WriteLine($"[BlockChain_public] 연결된 블록체인 ID 오류 : {chainId.Value} 해당 값으로 변경");
//                    _chainId = (int)chainId.Value;
//                }
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"[BlockChain_public] 연결 테스트 실패: {ex.Message}");
//                throw;
//            }
//        }

//        private void TimerInit()
//        {
//            _transactionMonitorTimer = new System.Timers.Timer(6000); // 60초마다 체크
//            _transactionMonitorTimer.Elapsed += MonitorPendingTransactions;
//            _transactionMonitorTimer.AutoReset = true;
//            _transactionMonitorTimer.Start();
//        }

//        private async Task StartEventMoniter()
//        {
//            try
//            {
//                if (_subscription == null)
//                {
//                    _monitoringCancel = new CancellationTokenSource();
//                    _subscription = new EthNewBlockHeadersSubscription(_wsClient);
//                    //TODO : 이벤트 발생시 유저에게 알림
//                    _subscription.SubscriptionDataResponse += async (sender, blockHeader) =>
//                    {
//                        try
//                        {
//                            var logs = await _web3Socket.Eth.Filters.GetLogs.SendRequestAsync(new NewFilterInput
//                            {
//                                FromBlock = new BlockParameter(blockHeader.Response.Number),
//                                ToBlock = new BlockParameter(blockHeader.Response.Number),
//                                Address = new[] { _contractAddress }
//                            });

//                            foreach (var log in logs)
//                            {
//                                await ProcessLog(log);
//                            }
//                            await CheckPendingTransaction(blockHeader.Response.Number);
//                        }
//                        catch (Exception ex)
//                        {
//                            Console.WriteLine($"[BlockChain_public] 이벤트 수신 중 오류: {ex.Message}");
//                        }
//                    };
//                    await _subscription.SubscribeAsync();
//                    Console.WriteLine($"[BlockChain_public] 이벤트 구독 완료");
//                }
//                else
//                {
//                    Console.WriteLine("[BlockChain_public] 블록 헤더 이벤트 구독이 이미 되어 있습니다.");
//                }
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"[BlockChain_public] 이벤트 구독 중 오류: {ex.Message}");
//                throw;
//            }
//        }

//        private async Task ProcessLog(FilterLog log)
//        {
//            try
//            {
//                _pendingTransactions.TryGetValue(log.TransactionHash, out var tracker);
//                if (tracker == null || tracker.data == null)
//                {
//                    Console.WriteLine($"[BlockChain_public] 트랜잭션 해시를 찾을 수 없습니다: {log.TransactionHash}");
//                    return;
//                }

//                var mintEvent = Event<ItemMintDTO>.DecodeEvent(log);
//                if (mintEvent != null)
//                {
//                    int tokenId = (int)mintEvent.Event.tokenId;
//                    int itemId = (int)mintEvent.Event.itemId;
//                    int serial = (int)mintEvent.Event.serial;

//                    Console.WriteLine($"[BlockChain_public] NFT 민팅 완료 : TokenId={mintEvent.Event.tokenId}, ItemId={mintEvent.Event.itemId}, serial={mintEvent.Event.serial}");

//                    try
//                    {
//                        var item = await DBManager.Instance.GetItem(itemId, serial);
//                        if (item != null)
//                        {
//                            await DBManager.Instance.UpdateItemToken(itemId, serial, tokenId, 1, tracker.data.reason, tracker.data.timestamp);
//                            Console.WriteLine($"[BlockChain_public] NFT 사용가능 및 세부 정보 변경 완료");

//                            Console.WriteLine($"\n[BlockChain_public] OpenSea 데이터 조회");
//                            //await RefreshOpenSeaCollection();
//                            Console.WriteLine($"\n[BlockChain_public] OpenSea 데이터 조회 - tokenID");
//                            await RefreshOpenSeaMetadata(tokenId);
//                        }
//                        else
//                        {
//                            Console.WriteLine($"[BlockChain_public] NFT 민팅은 성공했으나 DB 추가 실패");
//                            //TODO : 클라이언트에게 실패 진행
//                        }
//                        return;
//                    }
//                    catch (Exception ex)
//                    {
//                        Console.WriteLine($"[BlockChain_public] NFT 민팅 처리 중 오류: {ex.Message}");
//                    }
//                }

//                var burnEvent = Event<ItemBurnDTO>.DecodeEvent(log);
//                if (burnEvent != null)
//                {
//                    int itemId = (int)burnEvent.Event.itemId;
//                    int serial = (int)burnEvent.Event.serial;

//                    Console.WriteLine($"[BlockChain] NFT 소각 완료 : TokenId={burnEvent.Event.tokenId}");
//                    Console.WriteLine($"[BlockChain_public] NFT 제거 완료");
//                    try
//                    {
//                        //string json = JsonSerializer.Serialize(new
//                        //{
//                        //    shop_sell_response = new
//                        //    {
//                        //        id = itemId,
//                        //        uniqueid = serial,
//                        //        count = 1,
//                        //        isSuccess = true
//                        //    }
//                        //}, Startup.jsonOptions);
//                        await DBManager.Instance.Delete_Item(itemId, serial, -1);
//                        Console.WriteLine($"[BlockChain_public] DB 적용 완료 ID : {itemId} serial : {serial}");
//                        //TODO : 클라이언트에게 성공 진행
//                    }
//                    catch (Exception ex)
//                    {
//                        Console.WriteLine($"[BlockChain_public] NFT 소각 처리 중 오류: {ex.Message}");
//                    }

//                    return;
//                }

//                var tradeEvent = Event<TradeSuccessEventDTO>.DecodeEvent(log);
//                if (tradeEvent != null)
//                {
//                    var from = tradeEvent.Event.User1;
//                    var to = tradeEvent.Event.User2;

//                    Console.WriteLine($"[BlockChain] NFT 교환 완료: From={from}, To={to}");
//                    await TradeManager.Instance.ChainComplete(from, to);
//                }
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"[BlockChain_public] 이벤트 처리 중 오류: {ex.Message}");
//            }
//        }

//        private async Task CheckPendingTransaction(Nethereum.Hex.HexTypes.HexBigInteger blockNumber)
//        {
//            try
//            {
//                var block = await _web3Socket.Eth.Blocks.GetBlockWithTransactionsByNumber.SendRequestAsync(new BlockParameter(blockNumber)); // 블럭 번호 정보

//                if (block?.Transactions == null || !block.Transactions.Any())
//                    return;

//                foreach (var tx in block.Transactions) // 블록 트랜잭션 순회
//                {
//                    if (_pendingTransactions.TryRemove(tx.TransactionHash, out var tracker)) // 해시 확인
//                    {
//                        var receipt = await _web3Socket.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(tx.TransactionHash);

//                        if (receipt != null)
//                        {
//                            Console.WriteLine($"[BlockChain_public] 트랜잭션 확인됨: {tx.TransactionHash}, 상태: {(receipt.Status.Value == 1 ? "성공" : "실패")}");

//                            if (receipt.Status.Value == 1)
//                            {
//                                tracker.CompletionSource.TrySetResult(receipt);
//                                //if (tracker.data != null) await OnTransaction(receipt, tracker.data);
//                            }
//                            else
//                            {
//                                tracker.CompletionSource.TrySetException(new Exception($"[BlockChain_public] 트랜잭션 실패: {tx.TransactionHash}"));
//                                //if (tracker.data != null) await OnTransaction(tx.TransactionHash, tracker.data);
//                                if (tracker.data != null) await OnTransaction(tracker.data);
//                            }
//                        }
//                    }
//                }
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"[BlockChain] 블록 트랜잭션 확인 중 오류: {ex.Message}");
//            }
//        }

//        private async Task OnTransaction(TransactionReceipt receipt, TransactionMetadata data) // 트랜잭션 완료시 처리 -> processLog로 대체
//        {

//        }

//        private async Task OnTransaction(TransactionMetadata data) // 트랜잭션 실패시 처리
//        {
//            if (data.status == FuncType.MINT)
//            {
//                await DBManager.Instance.Delete_Item(data.uid, data.itemId, data.uniqueId, data.count);
//                Console.WriteLine($"[BlockChain_public] 민팅 실패 : 아이템={data.itemId}, UniqueId={data.uniqueId}");
//                //TODO : 클라이언트 실패 전송(아이템 삭제)
//            }
//            else if (data.status == FuncType.BURN)
//            {
//                Console.WriteLine($"[BlockChain_public] Brun 실패 : 아이템={data.itemId}, UniqueId={data.uniqueId}");
//            }
//            else if (data.status == FuncType.TRADE)
//            {
//                Console.WriteLine($"[BlockChain_public] 교환 실패");
//            }
//            else if (data.status == FuncType.UPGRADE)
//            {
//                Console.WriteLine($"[BlockChain_public] 업그레이드 실패");
//            }
//        }

//        private void MonitorPendingTransactions(object sender, System.Timers.ElapsedEventArgs e)
//        {
//            var expiredTxs = _pendingTransactions
//                .Where(pair => (DateTime.UtcNow - pair.Value.StartTime).TotalSeconds > _transactionTimeoutSeconds)
//                .ToList();

//            foreach (var tx in expiredTxs)
//            {
//                Console.WriteLine($"[BlockChain_public] 트랜잭션 타임아웃: {tx.Key}");
//                tx.Value.CompletionSource.TrySetException(
//                    new TimeoutException($"Transaction timeout: {tx.Key}")
//                );
//                _pendingTransactions.TryRemove(tx.Key, out _);
//            }
//        }

//        public async Task<bool> Request<T>(TransactionMetadata data, T func) where T : FunctionMessage
//        {
//            await ClearStuckNonceIfNeeded();
//            Console.WriteLine($"[BlockChain_public] NFT 트랜잭션 생성중");
//            string userAddress = (await DBManager.Instance.GetAddressByUID(data.uid))?.address;
//            if (string.IsNullOrEmpty(userAddress))
//            {
//                Console.WriteLine("[BlockChain_public] 유저 주소를 찾을 수 없습니다.");
//                Console.WriteLine("[BlockChain_public] 작업을 중지합니다.");
//                return false;
//            }

//            TransactionMetadata meta = await SetTransactionData<T>(data, func, userAddress);
//            if (meta == null)
//            {
//                Console.WriteLine("[BlockChain_public] 트랜잭션 메타데이터 생성 실패");
//                Console.WriteLine("[BlockChain_public] 작업을 중지합니다.");
//                return false;
//            }
//            try
//            {
//                string _txHash = null;
//                //if (meta.status == FuncType.TRADE) // 유저가 트랜잭션 실행
//                //{
//                //    _txHash = JsonSerializer.Serialize(new
//                //    {
//                //        signrequest_user = new
//                //        {
//                //            from = meta.From,
//                //            to = meta.To,
//                //            toAddress = meta.toAddress,
//                //            data = meta.Data,
//                //            type = data.status,
//                //            ChainId = _chainId,
//                //            dataHash = "0x" + Convert.ToHexString(((dynamic)func).dataHash).ToLower(),
//                //            gas = new Nethereum.Hex.HexTypes.HexBigInteger(meta.GasLimit).ToString()
//                //        }
//                //    }, Startup.jsonOptions);
//                //} 
//                //else // 서버 직접 실행 
//                {
//                    var account = await DBManager.Instance.GetAddressByUID(data.uid);
//                    var handler = _web3Http.Eth.GetContractHandler(_contractAddress);
//                    Console.WriteLine($"[BlockChain_public] 서버가 직접 트랜잭션 실행: {meta.status}");
//                    try
//                    {
//                        Console.WriteLine($"[BlockChain_public] 서버 계정 ETH 잔액: {await _web3Http.Eth.GetBalance.SendRequestAsync(_accountManager._serverAccount_public.Address)}");

//                        // 블록체인 탐색기 결과와 비교를 위한 자세한 로그
//                        Console.WriteLine($"[BlockChain_public] 계정 주소: {_accountManager._serverAccount_public.Address}");

//                        //var finalNonce = new HexBigInteger(BigInteger.Max(latest.Value, pending.Value));
//                        var finalNonce = await GetSafeNonce();
//                        var priorityFeeRaw = await _web3Http.Client.SendRequestAsync<string>("eth_maxPriorityFeePerGas", null);
//                        var priorityFee = new HexBigInteger(priorityFeeRaw);
//                        var feeHistory = await _web3Http.Eth.FeeHistory.SendRequestAsync(
//                            new HexBigInteger(1),
//                            BlockParameter.CreateLatest(),
//                            null
//                        );
//                        var baseFee = feeHistory.BaseFeePerGas[0].Value;
//                        var maxFee = baseFee + (priorityFee.Value * 12 / 10);

//                        HexBigInteger gasLimit = null;

//                        if (func is MintitemFunction mintFuncTyped)
//                        {
//                            var gasEstimate = await handler.EstimateGasAsync<MintitemFunction>(mintFuncTyped);
//                            mintFuncTyped.Nonce = finalNonce;
//                            mintFuncTyped.MaxFeePerGas = new HexBigInteger(maxFee);
//                            mintFuncTyped.MaxPriorityFeePerGas = priorityFee;
//                            mintFuncTyped.Gas = await handler.EstimateGasAsync(mintFuncTyped);
//                            mintFuncTyped.FromAddress = _accountManager._serverAccount_public.Address;
//                            Console.WriteLine($"[BlockChain_public] MINT 트랜잭션 데이터: {BitConverter.ToString(mintFuncTyped.GetCallData()).Replace("-", "")}");

//                            try
//                            {
//                                var _tx = await handler.SendRequestAsync<MintitemFunction>(mintFuncTyped);
//                                Console.WriteLine($"[BlockChain_public] MINT 트랜잭션 전송됨: {_tx}");
//                                TrackTransaction(_tx, meta);
//                                return true;
//                            }
//                            catch(Exception ex)
//                            {
//                                Console.WriteLine($"[BlockChain_public] MINT 트랜잭션 데이터 생성 실패: {ex}");
//                                return false;
//                            }
//                        }
//                        else if (func is ItemBurnFunction burnFuncTyped)
//                        {
//                            burnFuncTyped.Nonce = finalNonce;
//                            burnFuncTyped.MaxFeePerGas = new HexBigInteger(maxFee);
//                            burnFuncTyped.MaxPriorityFeePerGas = priorityFee;
//                            burnFuncTyped.Gas = await handler.EstimateGasAsync(burnFuncTyped);
//                            burnFuncTyped.FromAddress = _accountManager._serverAccount_public.Address;
//                            Console.WriteLine($"[BlockChain_public] BURN 트랜잭션 데이터: {BitConverter.ToString(burnFuncTyped.GetCallData()).Replace("-", "")}");

//                            try
//                            {
//                                var _tx = await handler.SendRequestAsync<ItemBurnFunction>(burnFuncTyped);
//                                Console.WriteLine($"[BlockChain_public] BURN 트랜잭션 전송됨: {_tx}");
//                                TrackTransaction(_tx, meta);
//                                return true;
//                            }
//                            catch(Exception ex)
//                            {
//                                Console.WriteLine($"[BlockChain_public] BURN 트랜잭션 데이터 생성 실패: {ex}");
//                                return false;
//                            }
//                        }
//                        else if (func is TradeItemsByServerFunction tradeFuncTyped)
//                        {
//                            tradeFuncTyped.Nonce = finalNonce;
//                            tradeFuncTyped.MaxFeePerGas = new HexBigInteger(maxFee);
//                            tradeFuncTyped.MaxPriorityFeePerGas = priorityFee;
//                            tradeFuncTyped.Gas = await handler.EstimateGasAsync(tradeFuncTyped);
//                            tradeFuncTyped.FromAddress = _accountManager._serverAccount_public.Address;
//                            Console.WriteLine($"[BlockChain_public] Trade 트랜잭션 데이터: {BitConverter.ToString(tradeFuncTyped.GetCallData()).Replace("-", "")}");

//                            try
//                            {
//                                var _tx = await handler.SendRequestAsync<TradeItemsByServerFunction>(tradeFuncTyped);
//                                Console.WriteLine($"[BlockChain_public] Trade 트랜잭션 전송됨: {_tx}");
//                                TrackTransaction(_tx, meta);
//                                return true;
//                            }
//                            catch (Exception ex)
//                            {
//                                Console.WriteLine($"[BlockChain_public] BURN 트랜잭션 데이터 생성 실패: {ex}");
//                                return false;
//                            }
//                        }
//                        Console.WriteLine($"[BlockChain_public] 예상 가스비: {UnitConversion.Convert.FromWei(gasLimit.Value * maxFee, UnitConversion.EthUnit.Ether)} ETH");
//                    }
//                    catch (Exception ex)
//                    {
//                        Console.WriteLine($"[BlockChain_public] 트랜잭션 전송 중 오류 발생: {ex.ToString()}");
//                        if (ex.InnerException != null)
//                        {
//                            Console.WriteLine($"[BlockChain_public] 내부 오류: {ex.InnerException.Message}");
//                        }
//                        return false;
//                    }
//                }
//                if (_txHash == null)
//                {

//                    Console.WriteLine($"[BlockChain_public] 트랜잭션 Null");
//                    return false;
//                }
//                Startup._SendToClient(data.uid, _txHash);
//                //TrackTransaction(_txHash, meta);
//                Console.WriteLine($"[BlockChain_public] 트랜잭션 데이터 유저 전달 성공");
//                return true;
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"[BlockChain_public] 트랜잭션 데이터 생성 실패: {ex}");
//                return false;
//            }
//        }
       
//        private async Task<HexBigInteger> GetSafeNonce()
//        {
//            var latest = await _web3Http.Eth.Transactions.GetTransactionCount.SendRequestAsync(
//                _accountManager._serverAccount_public.Address, BlockParameter.CreateLatest());

//            var pending = await _web3Http.Eth.Transactions.GetTransactionCount.SendRequestAsync(
//                _accountManager._serverAccount_public.Address, BlockParameter.CreatePending());

//            return new HexBigInteger(BigInteger.Max(latest.Value, pending.Value));
//        }

//        public async Task<TransactionMetadata> SetTransactionData<T>(TransactionMetadata data, T func, string address) where T : FunctionMessage
//        {
//            try
//            {
//                Console.WriteLine($"[BlockChain_public] NFT 트랜잭션 데이터 재구성");

//                switch(func)
//                {
//                    case MintitemFunction mintFunc:
//                        mintFunc.to = address;
//                        data.From = _accountManager._serverAccount_public.Address;
//                        var dataHash = GetItemDataHash(data);
//                        var signature = GetSign(dataHash);
//                        mintFunc.dataHash = dataHash;
//                        mintFunc.signature = signature;
//                        break;
//                    case ItemBurnFunction burnFunc:
//                        burnFunc.from = address;
//                        data.From = address;
//                        break;

//                    case TradeItemsByServerFunction transferFunc:
//                        transferFunc.User1 = (await DBManager.Instance.GetAccountByPrivateAddress(transferFunc.User1)).address;
//                        transferFunc.User2 = (await DBManager.Instance.GetAccountByPrivateAddress(transferFunc.User2)).address;
//                        data.From = _accountManager._serverAccount_public.Address;

//                        //transferFunc.signature = new byte[65];
//                        break;
//                    default:
//                        Console.WriteLine($"[BlockChain_public] 지원하지 않는 트랜잭션 타입: {func.GetType()}");
//                        return null;
//                }

//                byte[] functionDataBytes = func.GetCallData<T>();
//                string functionData = "0x" + BitConverter.ToString(functionDataBytes).Replace("-", "").ToLowerInvariant();

//                var gasEstimate = await _web3Http.Eth.Transactions
//                .EstimateGas.SendRequestAsync(new CallInput
//                {
//                    From = _accountManager._serverAccount_public.Address,
//                    To = _contractAddress,
//                    Data = functionData
//                });

//                data.To = _contractAddress;
//                data.toAddress = address;
//                data.Data = functionData;
//                data.GasLimit = gasEstimate.Value;

//                return data;
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"[BlockChain_public] NFT 트랜잭션 데이터 생성 실패: {ex}");
//                return null;
//            }
//        }

//        public async Task<string> SubmitSignedTransaction(string signedTransaction) // 트랜잭션 제출
//        {
//            try
//            {
//                var txHash = await _web3Http.Eth.Transactions.SendRawTransaction.SendRequestAsync(signedTransaction);
//                Console.WriteLine($"[BlockChain_public] 서명된 트랜잭션 제출 완료: {txHash}");

//                return txHash;
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"[BlockChain_public] 서명된 트랜잭션 제출 실패: {ex.Message}");
//                return null;
//            }
//        }

//        public void TrackTransaction(string txHash, TransactionMetadata metadata)// 트랜잭션 상태 추적 설정
//        {
//            var receiptTcs = new TaskCompletionSource<TransactionReceipt>();
//            var tracker = new TransactionStatusTracker(receiptTcs);
//            tracker.data = metadata;
//            _pendingTransactions[txHash] = tracker;
//            Console.WriteLine($"[BlockChain_public] 트랜잭션 추적: {txHash}");
//        }

//        public async Task<bool> VerifyItem(int uid, int tokenId, byte[] signature) // 
//        {
//            var web3 = new Web3(_web3Http.Client);
//            var handler = web3.Eth.GetContractHandler(_contractAddress);

//            var function = new VerifyItemFunction
//            {
//                tokenId = tokenId,
//                signature = signature
//            };

//            return await handler.QueryAsync<VerifyItemFunction, bool>(function);
//        }
//        private byte[] GetItemDataHash(int itemId, int serial, string reason, BigInteger timestamp, int version = 1)
//        {
//            var encoder = new ABIEncode();
//            var values = new List<ABIValue>
//            {
//                new ABIValue("address", _accountManager._serverAccount_public.Address),
//                new ABIValue("uint256", 2025), // 현재 서버의 체인이 아닌 프라이빗으로 하여 추측하지 못하도록
//                new ABIValue("uint256", itemId),
//                new ABIValue("uint256", serial),
//            };
//            values.Add(new ABIValue("uint256", version));
//            values.Add(new ABIValue("string", reason));
//            values.Add(new ABIValue("uint256", timestamp));

//            var encoded = encoder.GetABIEncoded(values.ToArray());
//            return Sha3Keccack.Current.CalculateHash(encoded);
//        }

//        private byte[] GetItemDataHash(TransactionMetadata meta)
//        {
//            var encoder = new ABIEncode();
//            var values = new List<ABIValue>
//            {
//                new ABIValue("address", _accountManager._serverAccount_public.Address),
//                new ABIValue("uint256", 2025), // 현재 서버의 체인이 아닌 프라이빗으로 하여 추측하지 못하도록
//                new ABIValue("uint256", meta.itemId),
//                new ABIValue("uint256", meta.uniqueId),
//            };
//            values.Add(new ABIValue("uint256", meta.version));
//            values.Add(new ABIValue("string", meta.reason));
//            values.Add(new ABIValue("uint256", meta.timestamp));

//            var encoded = encoder.GetABIEncoded(values.ToArray());
//            return Sha3Keccack.Current.CalculateHash(encoded);
//        }

//        private byte[] GetSign(byte[] dataHash)
//        {
//            var prefix = Encoding.UTF8.GetBytes("\x19Ethereum Signed Message:\n32");
//            var ethMessage = Sha3Keccack.Current.CalculateHash(prefix.Concat(dataHash).ToArray());
//            var signatureECDSA = new EthereumMessageSigner().Sign(dataHash, _accountManager._serverAccount_public.PrivateKey);

//            var signature = signatureECDSA.HexToByteArray();
//            return signature;
//        }
//        public bool VerifyClientSignature(string userAddress, byte[] dataHash, string signature)
//        {
//            var prefix = Encoding.UTF8.GetBytes("\x19Ethereum Signed Message:\n32");
//            var ethSignedMessage = Sha3Keccack.Current.CalculateHash(prefix.Concat(dataHash).ToArray());

//            var signer = new EthereumMessageSigner().EcRecover(ethSignedMessage, signature);
//            return signer.Equals(userAddress, StringComparison.OrdinalIgnoreCase);
//        }
//        public async Task GetOpenSeaCollectionNFTs(string chain = "sepolia")
//        {
//            try
//            {
//                using var httpClient = new HttpClient();

//                // 컬렉션의 NFT 목록 조회 (v2)
//                var url = $"https://api.opensea.io/api/v2/chain/{chain}/contract/{_contractAddress}/nfts";

//                var response = await httpClient.GetAsync(url);

//                Console.WriteLine($"[OpenSea] 컬렉션 NFT 목록 조회 결과: {response.StatusCode}");

//                if (response.IsSuccessStatusCode)
//                {
//                    var content = await response.Content.ReadAsStringAsync();

//                    using var jsonDoc = JsonDocument.Parse(content);
//                    var root = jsonDoc.RootElement;

//                    if (root.TryGetProperty("nfts", out var nftsElement) && nftsElement.ValueKind == JsonValueKind.Array)
//                    {
//                        Console.WriteLine($"[OpenSea] 컬렉션에서 {nftsElement.GetArrayLength()}개의 NFT 찾음");

//                        foreach (var nft in nftsElement.EnumerateArray())
//                        {
//                            if (nft.TryGetProperty("identifier", out var idElement) &&
//                                nft.TryGetProperty("name", out var nameElement))
//                            {
//                                Console.WriteLine($"- Token ID: {idElement.GetString()}, 이름: {nameElement.GetString()}");
//                            }
//                        }
//                    }
//                    else
//                    {
//                        Console.WriteLine("[OpenSea] NFT 목록을 찾을 수 없습니다.");
//                    }

//                    // 원본 응답 저장 (디버깅용)
//                    await File.WriteAllTextAsync($"opensea_collection_nfts_{_contractAddress}.json", content);
//                }
//                else
//                {
//                    var error = await response.Content.ReadAsStringAsync();
//                    Console.WriteLine($"[OpenSea] 오류 내용: {error}");
//                }
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"[OpenSea] API 호출 중 오류: {ex.Message}");
//            }
//        }

//        public async Task RefreshOpenSeaMetadata(int tokenId, string chain = "sepolia")
//        {
//            try
//            {
//                using var httpClient = new HttpClient();

//                // 필수 헤더 추가 (User-Agent)
//                httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");

//                // OpenSea 테스트넷 API URL (메타데이터 강제 갱신)
//                var url = $"https://testnets-api.opensea.io/api/v2/chain/{chain}/contract/{_contractAddress}/nfts/{tokenId}/refresh";

//                // POST 요청으로 메타데이터 갱신 트리거
//                var response = await httpClient.PostAsync(url, null);

//                Console.WriteLine($"[OpenSea] 메타데이터 갱신 요청 결과: {response.StatusCode}");

//                if (response.IsSuccessStatusCode)
//                {
//                    var content = await response.Content.ReadAsStringAsync();
//                    Console.WriteLine($"[OpenSea] 메타데이터 갱신 응답: {content}");

//                    // 디버깅용 저장
//                    await File.WriteAllTextAsync($"opensea_refresh_{tokenId}.json", content);
//                }
//                else
//                {
//                    var error = await response.Content.ReadAsStringAsync();
//                    Console.WriteLine($"[OpenSea] 갱신 실패 응답: {response.StatusCode}");
//                    Console.WriteLine($"[OpenSea] 오류 내용: {error}");
//                }
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"[OpenSea] 메타데이터 갱신 중 예외 발생: {ex.Message}");
//            }
//        }
//        public async Task RefreshAllNFTsInCollection(string chain = "sepolia")
//        {
//            try
//            {
//                using var httpClient = new HttpClient();
//                httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");

//                string baseUrl = $"https://testnets-api.opensea.io/api/v2/chain/{chain}/contract/{_contractAddress}/nfts";
//                string? next = null;

//                do
//                {
//                    string url = string.IsNullOrEmpty(next) ? baseUrl : $"{baseUrl}?next={next}";
//                    var res = await httpClient.GetAsync(url);
//                    string content = await res.Content.ReadAsStringAsync();
//                    using var json = JsonDocument.Parse(content);

//                    var root = json.RootElement;
//                    if (root.TryGetProperty("nfts", out var nfts))
//                    {
//                        foreach (var nft in nfts.EnumerateArray())
//                        {
//                            string tokenId = nft.GetProperty("identifier").GetString();
//                            await RefreshSingleNFT(tokenId, chain);
//                            await Task.Delay(300); // 과도한 요청 방지
//                        }
//                    }

//                    next = root.TryGetProperty("next", out var nextProp) ? nextProp.GetString() : null;

//                } while (!string.IsNullOrEmpty(next));
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"[OpenSea] 전체 NFT 새로고침 실패: {ex.Message}");
//            }
//        }

//        private async Task RefreshSingleNFT(string tokenId, string chain)
//        {
//            try
//            {
//                using var httpClient = new HttpClient();
//                httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");

//                string url = $"https://testnets-api.opensea.io/api/v2/chain/{chain}/contract/{_contractAddress}/nfts/{tokenId}/refresh";
//                var response = await httpClient.PostAsync(url, null);
//                Console.WriteLine($"[OpenSea] Token {tokenId} 새로고침 상태: {response.StatusCode}");
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"[OpenSea] Token {tokenId} 새로고침 실패: {ex.Message}");
//            }
//        }

//    }
//}
