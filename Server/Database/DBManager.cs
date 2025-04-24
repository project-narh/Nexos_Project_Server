using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Dapper;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using MySqlConnector.Logging;
using Server.Manager;
using Server.Packet;
using static Server.Packet.WebPacketHandler;
using Server.BlockChain;
using System.Numerics;
using Microsoft.EntityFrameworkCore.Storage;
using System.Reflection.Metadata.Ecma335;

namespace Server.Database
{
    class DBManager : ManagerInterface
    {
        public static DBManager Instance { get; } = new DBManager();

        private ServerdbContext _context = new ServerdbContext();

        public DBManager()
        {
            try
            {
                using (var connect = _context.CreateConnection())
                {
                    Console.WriteLine("MariaDB 연결 성공!");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("MariaDB 연결 실패: " + ex.Message);
            }
        }

        public void Init()
        {

        }

        #region 지갑 로그인
        public async Task<AddressAccount> GetAddressByUID(int uid) // uid로 지갑 조회
        {
            using var connect = _context.CreateConnection();

            return await connect.QueryFirstOrDefaultAsync<AddressAccount>(
                "SELECT * FROM address_account WHERE UID = @uid",
                new { uid });
        }
        public async Task<AddressAccount> GetAccountByAddress(string address) // 퍼블릭 주소로 지갑 조회
        {
            using var connect = _context.CreateConnection();

            return await connect.QueryFirstOrDefaultAsync<AddressAccount>(
                "SELECT * FROM address_account WHERE address = @address",
                new { address });
        }
        public async Task<AddressAccount> GetAccountByPrivateAddress(string prAddress)// 프라이빗 주소로 지갑 조회
        {
            using var connect = _context.CreateConnection();

            return await connect.QueryFirstOrDefaultAsync<AddressAccount>(
                "SELECT * FROM address_account WHERE pr_Address = @prAddress",
                new { prAddress });
        }
        public async Task<int?> GetUIDByAddress(string address)
        {
            using var connect = _context.CreateConnection();

            return await connect.ExecuteScalarAsync<int?>(
                "SELECT UID FROM address_account WHERE address = @address",
                new { address });
        }


        public async Task<AddressAccount> RegisterAddress(string address) // 지갑 등록
        {
            using var connect = _context.CreateConnection();

            var existing = await connect.QueryFirstOrDefaultAsync<AddressAccount>(
                "SELECT * FROM address_account WHERE address = @address", new { address });

            if (existing != null)
                return existing;

            int uid = await connect.ExecuteScalarAsync<int>(
        @"INSERT INTO address_account (address) VALUES (@address);
          SELECT LAST_INSERT_ID();", new { address });

            // 3. UID로 지갑 계정 생성 → pr_Address 확보
            string prAddress = BlockChainManager.Instance.CreatedBlockChain(uid);

            // 4. UPDATE로 pr_Address 저장
            await connect.ExecuteAsync(
                "UPDATE address_account SET pr_Address = @prAddress WHERE UID = @uid",
                new { prAddress, uid });

            // 5. 결과 조회 및 반환
            return await connect.QueryFirstAsync<AddressAccount>(
                "SELECT * FROM address_account WHERE UID = @uid", new { uid });
        }


        #endregion

        #region make item
        public UserItem MakeItem(UserItem item, int uid, int count)
        {
            UserItem useritem = new UserItem()
            {
                Name = item.Name,
                Id = item.Id,
                Count = count,
                Description = item.Description,
                Etc = item.Etc,
                //State = item.State,
                State = "LOCK",
                Type = item.Type,
                Uid = uid,
                Image = item.Image,
            }
            ;
            return useritem;
        }

        public UserItem MakeItem(UserItem item, int uid, int count,int tokenID,long timestamp, string reason)
        {
            UserItem useritem = new UserItem()
            {
                Name = item.Name,
                Id = item.Id,
                Count = count,
                Description = item.Description,
                Etc = item.Etc,
                //State = item.State,
                State = "LOCK",
                Type = item.Type,
                Uid = uid,
                Image = item.Image,
                Reason = reason,
                Timestamp = timestamp,
                TokenId = tokenID
            }
            ;
            return useritem;
        }

        public UserItem MakeItem(ItemList item, int uid, int count)
        {
            UserItem useritem = new UserItem()
            {
                Name = item.Name,
                Id = item.Id,
                Count = count,
                Description = item.Description,
                Etc = item.Etc,
                //State = item.State,
                State = "LOCK",
                Type = item.Type,
                Uid = uid,
                Image = item.Image,
            }
            ;
            return useritem;
        }

        public UserItem MakeItem(int id, int uid, int count)
        {
            var item = _context.ItemLists.FirstOrDefault(i => i.Id == id);
            UserItem useritem = new UserItem()
            {
                Name = item.Name,
                Id = item.Id,
                Count = count,
                Description = item.Description,
                Etc = item.Etc,
                //State = item.State,
                State = "LOCK",
                Type = item.Type,
                Image = item.Image,
                Uid = uid
            }
            ;
            return useritem;
        }

        public UserItem MakeItem(int id, int count)
        {
            var item = _context.ItemLists.FirstOrDefault(i => i.Id == id);
            UserItem useritem = new UserItem()
            {
                Name = item.Name,
                Id = item.Id,
                Count = count,
                Description = item.Description,
                Etc = item.Etc,
                //State = item.State,
                State = "LOCK",
                Type = item.Type,
                Image = item.Image
            }
            ;
            return useritem;
        }
        #endregion

        public bool GetHas(int uid, int item_id)
        {
            using (var connect = _context.CreateConnection())
            {
                string sql = "SELECT EXISTS (SELECT 1 FROM user_item WHERE uid = @Uid AND id = @ItemId)";
                return connect.ExecuteScalar<bool>(sql, new { Uid = uid, ItemId = item_id });
            }
        }

        public int GetItemCount(int uid, int item_id)
        {
            using (var connect = _context.CreateConnection())
            {
                string sql = "SELECT COALESCE(SUM(count), 0) FROM user_item WHERE uid = @Uid AND id = @ItemId";
                return connect.ExecuteScalar<int>(sql, new { Uid = uid, ItemId = item_id });
            }
        }

        #region item
        //public List<UserItem> Get_UserItem(int uid) // 아이템 리스트 로드
        //{
        //    Console.WriteLine($"{uid} 플레이어 : 아이템 목록");
        //    using (var connect = _context.CreateConnection())
        //    {
        //        Console.WriteLine($"{uid} 플레이어 : 아이템 목록");
        //        string query = "SELECT * FROM graduation_db.user_item WHERE uid = @uid";
        //        return connect.Query<UserItem>(query, new { uid }).ToList();
        //    }
        //    Console.WriteLine($"발견안됨");
        //    return null;
        //}

        public async Task<UserItem> GetItemByTokenId(int tokenId) // 토큰 ID로 아이템 조회 및 
        {
            return await _context.UserItems.FirstOrDefaultAsync(i => i.TokenId == tokenId);
        }

        public async Task<bool> UpdateItemToken(int itemId, int uniqueId, int tokenId, int version, string reason, long timestamp) // 토큰 ID, Verison, sign 업데이트
        {
            try
            {
                var item = await _context.UserItems
                    .FirstOrDefaultAsync(i => i.UniqueId == uniqueId && i.Id == itemId);

                if (item == null)
                {
                    Console.WriteLine("아이템을 찾을 수 없습니다.");
                    return false;
                }

                item.TokenId = tokenId;
                item.Version = version;
                item.Reason = reason;
                item.Timestamp = timestamp;
                item.State = "USE";
                _context.UserItems.Update(item);
                await _context.SaveChangesAsync();
                await MetadataFile(item);
                Console.WriteLine("아이템 토큰 정보 업데이트 성공");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("토큰 정보 업데이트 실패: " + ex.Message);
                return false;
            }
        }

        public async Task<bool> UpdateItem(int tokenId, int version, string reason, long timestamp) // 토큰 ID를 통해 버전과 서명 업데이트
        {
            try
            {
                var item = await _context.UserItems
                    .FirstOrDefaultAsync(i => i.TokenId == tokenId);

                if (item == null)
                {
                    Console.WriteLine("해당 tokenId의 아이템을 찾을 수 없습니다.");
                    return false;
                }

                item.Version = version;
                item.Reason = reason;
                item.Timestamp = timestamp;

                _context.UserItems.Update(item);
                await _context.SaveChangesAsync();
                await MetadataFile(item);
                Console.WriteLine("버전 및 서명 업데이트 성공");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("버전 및 서명 업데이트 실패: " + ex.Message);
                return false;
            }
        }

        public async Task<int?> GetTokenId(int itemId, int uniqueId) // 아이템 토큰 ID 조회
        {
            var item = await _context.UserItems
                .FirstOrDefaultAsync(i => i.Id == itemId && i.UniqueId == uniqueId);

            return item?.TokenId;
        }

        public async Task<UserItem> GetItem(int itemId, int uniqueId)
        {
            using (var connect = _context.CreateConnection())
            {
                string sql = "SELECT * FROM user_item WHERE id = @ItemId AND uniqueId = @UniqueId LIMIT 1";
                return await connect.QueryFirstOrDefaultAsync<UserItem>(sql, new { ItemId = itemId, UniqueId = uniqueId });
            }
        }

        public async Task<List<UserItem>> Get_UserItem(int uid) // 아이템 리스트 로드
        {
            try
            {
                Console.WriteLine($"{uid} 플레이어 : 아이템 목록 조회 시작");

                using (var connect = _context.CreateConnection())
                {
                    Console.WriteLine($"{uid} 플레이어 : DB 연결 성공");
                    string query = "SELECT * FROM graduation_db.user_item WHERE uid = @uid";
                    var items = (await connect.QueryAsync<UserItem>(query, new { uid })).ToList();

                    Console.WriteLine($"{uid} 플레이어 : 아이템 목록 조회 완료. 개수 = {items.Count}");
                    return items;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{uid} 플레이어 : 아이템 목록 조회 실패. 오류: {ex.Message}");
                return new List<UserItem>(); // 🔹 예외 발생 시 빈 리스트 반환
            }
        }


        public async Task<int> Create_Item(int uid, int item_id, int count) // 아이템 추가 (블록체인 등록을 위한 고유 번호 반환)
        {
            try
            {
                ItemList info = await _context.ItemLists.FirstOrDefaultAsync(n => n.Id == item_id);
                if (info == null)
                {
                    Console.WriteLine($"[구매] 아이템 코드 찾기 실패");
                    return -1;
                }

                UserItem userItem = MakeItem(info, uid, count);
                if (userItem == null)
                {
                    Console.WriteLine($"[구매] 아이템 생성 실패: MakeItem() 반환 값이 null");
                    return -1;
                }
                _context.ChangeTracker.AutoDetectChangesEnabled = false;
                var local = _context.UserItems.Local
                .FirstOrDefault(entry =>
                    entry.Id == userItem.Id &&
                    entry.UniqueId == 0 &&
                    entry.Uid == userItem.Uid);

                if (local != null)
                    _context.Entry(local).State = EntityState.Detached;
                await _context.UserItems.AddAsync(userItem);
                await _context.SaveChangesAsync();

                _context.ChangeTracker.AutoDetectChangesEnabled = true;

                await _context.SaveChangesAsync();
                var inserted = await _context.UserItems
                .Where(i => i.Uid == uid && i.Id == item_id)
                .OrderByDescending(i => i.UniqueId)
                .FirstOrDefaultAsync();
                //await MetadataFile(inserted);
                return inserted?.UniqueId ?? -1;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[구매] 아이템 생성중 오류 발생 :  {ex.Message}");
                return -1;
            }
        }

        public async Task<bool> Create_Item(UserItem item, int uid, int count) // 아이템 추가
        {
            if (item == null) return false;

            _context.UserItems.Add(MakeItem(item, uid, count));
            return await _context.SaveChangesAsync() > 0;
        }

        public async Task<bool> Create_Item_Trade(UserItem item, int uid, int count, string reason = "서버에 의한 아이템 생성") // 아이템 추가
        {
            if (item == null) return false;

            var newItem = MakeItem(item, uid, count, item.TokenId, item.Timestamp, reason);

            if (newItem == null) return false;

            _context.UserItems.Add(newItem);
            return true;
        }

        public async Task<bool> Create_Item(ItemList item, int uid, int count) // 아이템 추가
        {
            if (item == null) return false;

            _context.UserItems.Add(MakeItem(item, uid, count));
            return await _context.SaveChangesAsync() > 0;
        }

        public async Task<bool> Update_Item(int item_id, int uniqueId, int uid, ItemState state) // 아이템 업데이트
        {
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    var item = await _context.UserItems.FirstOrDefaultAsync(i => i.Id == item_id && i.UniqueId == uniqueId);
                    if (item == null)
                        throw new Exception("아이템을 찾을 수 없습니다.");

                    item.Uid = uid;
                    item.State = state.ToString();

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    Console.WriteLine("아이템 상태 변경");
                    return true;
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    Console.WriteLine("아이템 상태 실패: " + ex.Message);
                    return false;
                }
            }
        }

        public async Task<bool> Update_Item(int item_id, int uniqueId, ItemState state) // 아이템 업데이트
        {
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    var item = await _context.UserItems.FirstOrDefaultAsync(i => i.Id == item_id && i.UniqueId == uniqueId);
                    if (item == null)
                        throw new Exception("아이템을 찾을 수 없습니다.");

                    item.State = state.ToString();

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    Console.WriteLine("아이템 상태 변경");
                    return true;
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    Console.WriteLine("아이템 상태 실패: " + ex.Message);
                    return false;
                }
            }
        }

        public async Task<bool> Delete_Item(int uid, int item_id, int unique_id, int count)
        {
            Console.WriteLine($"비활성화 시도 {uid} {item_id} {unique_id}");
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    var item = await _context.UserItems.FirstOrDefaultAsync(i => i.Uid == uid && i.Id == item_id && i.UniqueId == unique_id);

                    if (item == null) throw new Exception("아이템을 찾을 수 없습니다.");
                    if (item.Count < count) throw new Exception("아이템 개수가 부족하여 삭제할 수 없습니다.");

                    if (item.Count == count) // 위에서 수량 체크했으니 더 많을 수 없음 즉 같음
                    {
                        //await _context.Database.ExecuteSqlRawAsync(
                        //"DELETE FROM user_item WHERE id = {0} AND uniqueId = {1} AND uid = {2}",
                        //item_id, unique_id, uid
                        //);
                        item.Count = 0;
                        item.State = "DESTROY";
                        item.Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                        item.Reason = "삭제";
                        _context.UserItems.Update(item);
                    }
                    else if (item.Count > count)
                    {
                        item.Count -= count;
                        _context.Update(item);
                    }
                    else
                    {
                        //혹시 모를 조건 추가
                        throw new Exception("아이템 개수가 부족합니다.");
                    }
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    Console.WriteLine("아이템 삭제 성공!");
                    return true;
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    Console.WriteLine("아이템 삭제 실패: " + ex.Message);
                    return false;
                }
            }
        }

        public async Task<bool> Delete_Item(int item_id, int unique_id, int count)
        {
            Console.WriteLine($"삭제 시도 {item_id} {unique_id} {count}");
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    var item = await _context.UserItems.FirstOrDefaultAsync(i => i.Id == item_id && i.UniqueId == unique_id);

                    if (item == null) throw new Exception("아이템을 찾을 수 없습니다.");

                    if(count == -1) count = item.Count;

                    if (item.Count < count) throw new Exception("아이템 개수가 부족하여 삭제할 수 없습니다.");

                    if (item.Count == count) // 위에서 수량 체크했으니 더 많을 수 없음 즉 같음
                    {
                        item.Count = 0;
                        item.State = "DESTROY";
                        item.Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                        item.Reason = "삭제";
                        _context.UserItems.Update(item);
                        //await _context.Database.ExecuteSqlRawAsync(
                        //"DELETE FROM user_item WHERE id = {0} AND uniqueId = {1} AND uid = {2}",
                        //item_id, unique_id, item.Uid
                        //);
                    }
                    else if (item.Count > count)
                    {
                        item.Count -= count;
                        _context.Update(item);
                    }
                    else
                    {
                        //혹시 모를 조건 추가
                        throw new Exception("아이템 개수가 부족합니다.");
                    }
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    Console.WriteLine("아이템 삭제 성공!");
                    return true;
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    Console.WriteLine("아이템 삭제 실패: " + ex.Message);
                    return false;
                }
            }
        }

         public async Task<bool> Trade_Item(int sender_uid, int receiver_uid, int item_id, int unique_id, int count) // 아이템 거래 (카운트 X)
        {
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    var item = await _context.UserItems.FirstOrDefaultAsync(i => i.Uid == sender_uid && i.Id == item_id && i.UniqueId == unique_id);

                    if (item == null) throw new Exception("아이템을 찾을 수 없습니다.");
                    if (item.Count < count) throw new Exception("거래하려는 아이템 개수가 부족합니다.");
                    if (sender_uid == receiver_uid) throw new Exception("자신에게 거래할 수 없습니다.");

                    var receiverItem = await _context.UserItems.FirstOrDefaultAsync(i => i.Uid == receiver_uid && i.Id == item_id);

                    if (receiverItem != null)
                    {
                        await Create_Item(item, receiver_uid, count);
                    }
                    else 
                    {
                        await Create_Item(item, receiver_uid, count);
                    }

                    if (item.Count == count)
                    {
                        await _context.Database.ExecuteSqlRawAsync(
                        "DELETE FROM user_item WHERE id = {0} AND uniqueId = {1} AND uid = {2}",
                        item_id, unique_id, sender_uid
                        );
                    }
                    else if (item.Count > count)
                    {
                        item.Count -= count;
                        _context.UserItems.Update(item);
                    }
                    else
                    {
                        throw new Exception("거래하려는 아이템 개수가 부족합니다.");
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                    Console.WriteLine("아이템 거래 성공");
                    return true;
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    Console.WriteLine("아이템 거래 실패 : " + ex.Message);
                    return false;
                }
            }
        }

        //public async Task<bool> Trade_Item(int sender_uid, int receiver_uid, int item_id, int unique_id, int count) // 아이템 거래 (카운트 X)
        //{
        //    using (var transaction = await _context.Database.BeginTransactionAsync())
        //    {
        //        try
        //        {
        //            var item = await _context.UserItems.FirstOrDefaultAsync(i => i.Uid == sender_uid && i.Id == item_id && i.UniqueId == unique_id);

        //            if (item == null) throw new Exception("아이템을 찾을 수 없습니다.");
        //            if (item.Count < count) throw new Exception("거래하려는 아이템 개수가 부족합니다.");
        //            if (sender_uid == receiver_uid) throw new Exception("자신에게 거래할 수 없습니다.");

        //            var receiverItem = await _context.UserItems.FirstOrDefaultAsync(i => i.Uid == receiver_uid && i.Id == item_id);

        //            if (receiverItem != null)
        //            {
        //                await Create_Item(item, receiver_uid, count);
        //            }
        //            else 
        //            {
        //                await Create_Item(item, receiver_uid, count);
        //            }

        //            if (item.Count == count)
        //            {
        //                await _context.Database.ExecuteSqlRawAsync(
        //                "DELETE FROM user_item WHERE id = {0} AND uniqueId = {1} AND uid = {2}",
        //                item_id, unique_id, sender_uid
        //                );
        //            }
        //            else if (item.Count > count)
        //            {
        //                item.Count -= count;
        //                _context.UserItems.Update(item);
        //            }
        //            else
        //            {
        //                throw new Exception("거래하려는 아이템 개수가 부족합니다.");
        //            }

        //            await _context.SaveChangesAsync();
        //            await transaction.CommitAsync();
        //            Console.WriteLine("아이템 거래 성공");
        //            return true;
        //        }
        //        catch (Exception ex)
        //        {
        //            await transaction.RollbackAsync();
        //            Console.WriteLine("아이템 거래 실패 : " + ex.Message);
        //            return false;
        //        }
        //    }
        //}

        //public async Task<bool> Trade_Item(int sender_uid, int receiver_uid, int item_id, int unique_id, int count) // 아이템 거래 (카운트 방식)
        //{
        //    using (var transaction = await _context.Database.BeginTransactionAsync())
        //    {
        //        try
        //        {
        //            var item = await _context.UserItems.FirstOrDefaultAsync(i => i.Uid == sender_uid && i.Id == item_id && i.UniqueId == unique_id);

        //            if (item == null) throw new Exception("아이템을 찾을 수 없습니다.");
        //            if (item.Count < count) throw new Exception("거래하려는 아이템 개수가 부족합니다.");
        //            if (sender_uid == receiver_uid) throw new Exception("자신에게 거래할 수 없습니다.");

        //            var receiverItem = await _context.UserItems.FirstOrDefaultAsync(i => i.Uid == receiver_uid && i.Id == item_id);

        //            if (receiverItem != null) // 받는 사람이 아이템 가지고 있는 경우
        //            {
        //                receiverItem.Count += count;
        //                _context.UserItems.Update(item);
        //                if (item.Count == count)
        //                {
        //                    await _context.Database.ExecuteSqlRawAsync(
        //                    "DELETE FROM user_item WHERE id = {0} AND uniqueId = {1} AND uid = {2}",
        //                    item_id, unique_id, sender_uid
        //                    );
        //                }
        //                else if (item.Count > count)
        //                {
        //                    item.Count -= count;
        //                    _context.UserItems.Update(item);
        //                }
        //                else
        //                {
        //                    throw new Exception("거래하려는 아이템 개수가 부족합니다.");
        //                }
        //            }
        //            else 
        //            {
        //                if (item.Count == count)
        //                {
        //                    _context.UserItems.Remove(item);

        //                }
        //                else if (item.Count > count)
        //                {
        //                    item.Count -= count;
        //                }
        //                else
        //                {
        //                    throw new Exception("거래하려는 아이템 개수가 부족합니다.");
        //                }
        //                Create_Item(item, receiver_uid, count);
        //            }

        //            await _context.SaveChangesAsync();
        //            await transaction.CommitAsync();
        //            Console.WriteLine("아이템 거래 성공");
        //            return true;
        //        }
        //        catch (Exception ex)
        //        {
        //            await transaction.RollbackAsync();
        //            Console.WriteLine("아이템 거래 실패 : " + ex.Message);
        //            return false;
        //        }
        //    }
        //}

        /// <summary>
        /// int sender_uid(송신자), int receiver_uid(수신자) , int item_id(아이템 코드), int unique_id(아이템 고유 코드), int count(수량)
        /// </summary>
        public async Task<bool> Trade_Item(int sender_uid, int receiver_uid, List<TradeItem> itemList, IDbContextTransaction transaction) // 아이템 거래
        {
            try
            {
                Console.WriteLine($"검색 조건: sender_uid={sender_uid}");

                // 해당 사용자의 아이템 목록 확인
                var userItems = await _context.UserItems
                    .Where(i => i.Uid == sender_uid)
                    .ToListAsync();

                Console.WriteLine($"사용자 {sender_uid}의 아이템 수: {userItems.Count}");
                foreach (var userItem in userItems)
                {
                    Console.WriteLine($"보유 아이템: Id={userItem.Id}, UniqueId={userItem.UniqueId}, Count={userItem.Count}");
                }

                foreach (TradeItem _titem in itemList)
                {
                    Console.WriteLine($"거래 아이템 : {_titem.itemId} {_titem.uniqueId} {_titem.count}");
                    var item = await _context.UserItems
                        .FirstOrDefaultAsync(i => i.Uid == sender_uid && i.Id == _titem.itemId && i.UniqueId == _titem.uniqueId);

                    if (item == null)
                    {
                        Console.WriteLine("아이템을 찾을 수 없습니다.");
                        return false;
                    }
                    if (item.Count < _titem.count)
                    {
                        Console.WriteLine("거래하려는 아이템 개수가 부족합니다.");
                        return false;
                    }
                    if (sender_uid == receiver_uid)
                    {
                        Console.WriteLine("자신에게 거래할 수 없습니다.");
                        return false;
                    }

                    if(item.Type == ItemType.EQUIPMENT.ToString())
                    {
                        item.Uid = receiver_uid;
                        _context.UserItems.Update(item);
                    }
                    else
                    {
                        var receiverItem = await _context.UserItems
                            .FirstOrDefaultAsync(i => i.Uid == receiver_uid && i.Id == _titem.itemId);

                        if (receiverItem != null || item.Type == ItemType.EQUIPMENT.ToString()) // 받는 사람이 아이템을 가지고 있는 경우
                        {
                            receiverItem.Count += _titem.count;
                            _context.UserItems.Update(receiverItem);

                            if (item.Count == _titem.count)
                            {
                                _context.UserItems.Remove(item);
                            }
                            else
                            {
                                item.Count -= _titem.count;
                                _context.UserItems.Update(item);
                            }
                        }
                        else // 받는 사람이 아이템을 가지고 있지 않은 경우
                        {
                            bool result;
                            if (item.Count == _titem.count)
                            {
                                item.Uid = receiver_uid;
                                _context.UserItems.Remove(item);
                                result = await Create_Item_Trade(item, receiver_uid, _titem.count);
                            }
                            else
                            {
                                item.Count -= _titem.count;
                                _context.UserItems.Update(item);
                                result = await Create_Item_Trade(item, receiver_uid, _titem.count, "아이템 거래로 인한 분할");
                            }
                            Console.WriteLine($"[거래] 새로운 아이탬 생성 : {result}");
                            if (!result) throw new Exception("아이템 생성 실패");
                        }
                    }
                    item.State = "LOCK";
                    _context.UserItems.Update(item);
                }
                Console.WriteLine("아이템 거래 성공");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("아이템 거래 실패 : " + ex.Message);
                return false;
            }
        }

        public async Task<bool> Trade_Items_Swap(int sender_uid, int receiver_uid,
                                               List<TradeItem> senderItems,
                                               List<TradeItem> receiverItems)
        {
            if ((senderItems == null || senderItems.Count == 0) && (receiverItems == null || receiverItems.Count == 0))
            {
                Console.WriteLine("거래할 아이템이 없습니다.");
                return false;
            }

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    if (senderItems != null && senderItems.Count > 0)
                    {
                        bool senderToReceiverSuccess = await Trade_Item(sender_uid, receiver_uid, senderItems, transaction);
                        if (!senderToReceiverSuccess)
                        {
                            await transaction.RollbackAsync();
                            return false;
                        }
                    }

                    if (receiverItems != null && receiverItems.Count > 0)
                    {
                        bool receiverToSenderSuccess = await Trade_Item(receiver_uid, sender_uid, receiverItems, transaction);
                        if (!receiverToSenderSuccess)
                        {
                            await transaction.RollbackAsync();
                            return false;
                        }
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                    Console.WriteLine("거래 성공");
                    return true;
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    Console.WriteLine("거래 실패: " + ex.Message);
                    return false;
                }
            }
        }
        #endregion

        #region Shop
        public async Task<(ShopList Shop, List<ItemList> Items)> GetShopItemList(int shopId)
        {
            try
            {
                using (var connection = _context.CreateConnection())
                {
                    string shopQuery = @"
                        SELECT * FROM shop_list 
                        WHERE ShopID = @ShopId
                        LIMIT 1";

                    var shop = await connection.QueryFirstOrDefaultAsync<ShopList>(
                        shopQuery, new { ShopId = shopId });

                    if (shop == null)
                    {
                        Console.WriteLine($"상점 ID {shopId}를 찾을 수 없습니다.");
                        return (null, new List<ItemList>());
                    }

                    string itemsQuery = @"
                        SELECT i.* FROM item_list i
                        INNER JOIN shop_item si ON i.id = si.ItemID
                        WHERE si.ShopID = @ShopId";

                    var items = (await connection.QueryAsync<ItemList>(
                        itemsQuery, new { ShopId = shopId })).ToList();

                    Console.WriteLine($"SQL: 상점 {shopId} 아이템 {items.Count}개 조회 완료");
                    return (shop, items);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"상점 정보 SQL 조회 중 오류 발생: {ex.Message}");
                return (null, new List<ItemList>());
            }
        }
        #endregion


        #region account
        public async Task<(bool, string)> RegisterAccount(string id, string pw, string nickname)
        {
            if (await _context.UserAccounts.AnyAsync(n => n.UserId == id)) return (false, "이미 존재하는 아이디입니다.");
            if (await _context.UserAccounts.AnyAsync(n => n.Nickname == nickname)) return (false, "이미 존재하는 닉네임입니다.");
            UserAccount userAccount = new UserAccount { UserId = id, UserPw = pw, Nickname = nickname };

            _context.UserAccounts.Add(userAccount);
            await _context.SaveChangesAsync();

            return (true, "회원가입에 성공하였습니다.");
        }

        public async Task<(int, string)> Login(string id, string pw)
        {
            using (var connect = _context.CreateConnection())
            {
                string query = $"SELECT uid, NICKNAME FROM graduation_db.user_account WHERE user_Id = @user_Id AND user_Pw = @password";
                var tryLogin = await connect.QueryFirstOrDefaultAsync<UserAccount>(query, new { user_Id = id, password = pw });

                if (tryLogin != null)
                {
                    Console.WriteLine($"로그인 성공 {tryLogin.Nickname}");
                    return (tryLogin.Uid, tryLogin.Nickname);
                }
            }
            Console.WriteLine($"로그인 실패");
            return (-1, "");
        }

        //public (bool, string) RegisterAccount(string id, string pw, string nickname)
        //{
        //    if (_context.UserAccounts.Any(n => n.UserId == id)) return (false, "이미 존재하는 아이디입니다.");
        //    if (_context.UserAccounts.Any(n => n.Nickname == nickname)) return (false, "이미 존재하는 닉네임입니다.");
        //    UserAccount userAccount = new UserAccount { UserId = id, UserPw = pw, Nickname = nickname };

        //    _context.UserAccounts.Add(userAccount);
        //    _context.SaveChanges();

        //    return (true, "회원가입에 성공하였습니다.");
        //}

        //public (int, string) Login(string id, string pw)
        //{
        //    using (var connect = _context.CreateConnection())
        //    {
        //        string query = $"SELECT uid, NICKNAME FROM graduation_db.user_account WHERE user_Id = @user_Id AND user_Pw = @password";
        //        var tryLogin = connect.QueryFirstOrDefault<UserAccount>(query, new { user_Id = id, password = pw });

        //        if (tryLogin != null)
        //        {
        //            Console.WriteLine($"로그인 성공 {tryLogin.Nickname}");
        //            return (tryLogin.Uid, tryLogin.Nickname);
        //        }
        //    }
        //    Console.WriteLine($"로그인 실패 ");
        //    return (-1, "");
        //}
        #endregion


        #region 메타데이터
        public async Task MetadataFile(UserItem item)
        {
            try
            {
                var metadata = new
                {
                    name = item.Name,
                    description = item.Description,
                    image = "https://metaongraduation.kro.kr/image/" + item.Image + ".png",
                    attributes = new List<object>
                    {
                        new { trait_type = "고유 ID", value = item.UniqueId.ToString() },
                        new { trait_type = "유형", value = item.Type },
                        new { trait_type = "버전", value = 1 },
                        new { trait_type = "상태", value = item.State }
                    }
                };

                string jsonContent = JsonSerializer.Serialize(metadata, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping // 한글 등 유니코드 문자 처리
                });

                string directoryPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "metadata");
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }
                string filePath = Path.Combine(directoryPath, $"{item.TokenId}.json");
                //string hexTokenId = item.TokenId.ToString("x").PadLeft(64, '0');
                //filePath = Path.Combine(directoryPath, $"{hexTokenId}.json");
                //if (item.Version == 0)
                //    filePath = Path.Combine(directoryPath, $"{item.Id}_{item.UniqueId}_{1}.json");
                //else
                //    filePath = Path.Combine(directoryPath, $"{item.Id}_{item.UniqueId}_{item.Version}.json");
                await File.WriteAllTextAsync(filePath, jsonContent);

                Console.WriteLine($"[Metadata] 메타데이터 파일 생성 완료: {filePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Metadata] 메타데이터 파일 생성 실패: {ex.Message}");
            }
        }
        #endregion

    }
}
