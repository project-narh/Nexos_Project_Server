using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Nethereum.Web3.Accounts;
using Server.Database;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Server.BlockChain
{
    public enum AccountType
    {
        Server_Public,
        Server_Private,
        User
    }

    public class AccountManager
    {
        private readonly string _keyDirPublic = "./server-keys_public";
        private readonly string _keyDirPrivate = "./server-keys_private";
        private readonly string _userKeyDir = "./user-keys";
        private readonly string _userPasswordDir = "./user-passwords";
        private readonly string _serverAccountName = "server-account.json";
        public Account _serverAccount_public;
        public Account _serverAccount_private;

        public AccountManager()
        {
            Console.WriteLine("[BlockChain] 계정 관리자 로드");
            if (!Directory.Exists(_keyDirPublic))
            {
                Console.WriteLine("[BlockChain] public 없는 것 확인 생성");
                Directory.CreateDirectory(_keyDirPublic);
            }
            if (!Directory.Exists(_keyDirPrivate))
            {
                Console.WriteLine("[BlockChain] private 없는 것 확인 생성");
                Directory.CreateDirectory(_keyDirPrivate);
            }
            if (!Directory.Exists(_userKeyDir))
            {
                Directory.CreateDirectory(_userKeyDir);
            }
            if (!Directory.Exists(_userPasswordDir))
            {
                Directory.CreateDirectory(_userPasswordDir);
            }

            _serverAccount_public = LoadAccount(AccountType.Server_Public);
            _serverAccount_private = LoadAccount(AccountType.Server_Private);
            Console.WriteLine($"[BlockChain] public 관리자 주소 : {_serverAccount_public.Address}");
            Console.WriteLine($"[BlockChain] private 관리자 주소 : {_serverAccount_private.Address}");
        }

        private string GetKeyPath(AccountType type, int uid = 0)
        {
            return type switch
            {
                AccountType.Server_Public => Path.Combine(_keyDirPublic, _serverAccountName),
                AccountType.Server_Private => Path.Combine(_keyDirPrivate, _serverAccountName),
                _ => Path.Combine(_userKeyDir, $"{uid}.json")
            };
        }

        private string GetPasswordPath(AccountType type, int uid = 0)
        {
            return type switch
            {
                AccountType.Server_Public => Path.Combine(_keyDirPublic, "server_password.json"),
                AccountType.Server_Private => Path.Combine(_keyDirPrivate, "server_password.json"),
                _ => Path.Combine(_userPasswordDir, $"{uid}.json")
            };
        }

        private string CreatePassword(int length = 16)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*()";
            var random = new Random();
            var stringBuilder = new StringBuilder(length);

            for (int i = 0; i < length; i++)
            {
                stringBuilder.Append(chars[random.Next(chars.Length)]);
            }

            return stringBuilder.ToString();
        }

        public string GetPassword(AccountType type, int uid = 0)
        {
            string path = GetPasswordPath(type, uid);

            if (!File.Exists(path))
            {
                string password = CreatePassword(16);
                var data = new { password };
                string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
                return password;
            }

            string existingJson = File.ReadAllText(path);
            return JsonSerializer.Deserialize<AccountPassword>(existingJson).password;
        }


        public Account CreateAccount(AccountType type, string password, int uid = 0)
        {
            var eckey = Nethereum.Signer.EthECKey.GenerateKey();
            var privateKey = eckey.GetPrivateKeyAsBytes().ToHex(true);
            var account = new Account(privateKey);

            Console.WriteLine($"[BlockChain] {((type == AccountType.Server_Public || type == AccountType.Server_Private) ? "서버" : $"UID {uid}")} 계정 생성: {account.Address}");

            var keyStore = new Nethereum.KeyStore.KeyStoreService();
            var json = keyStore.EncryptAndGenerateDefaultKeyStoreAsJson(password, eckey.GetPrivateKeyAsBytes(), account.Address);

            File.WriteAllText(GetKeyPath(type, uid), json);
            return account;
        }

        public Account LoadAccount(AccountType type, int uid = 0)
        {
            string password = GetPassword(type, uid);
            string path = GetKeyPath(type, uid);

            if (!File.Exists(path))
            {
                Console.WriteLine($"[BlockChain] 계정 확인 UID : {uid}");
                return CreateAccount(type, password, uid);
            }
            
            try
            {
                string keyStoreFile = File.ReadAllText(path);

                var keyStore = new Nethereum.KeyStore.KeyStoreService();
                byte[] privateKeyBytes = keyStore.DecryptKeyStoreFromJson(password, keyStoreFile);
                string privateKey = privateKeyBytes.ToHex(true);
                Console.WriteLine($"[BlockChain] uid {uid}  type : {type} private : {privateKey}");
                return new Account(privateKey);
            }
            catch(Exception ex)
            {
                Console.WriteLine($"[BlockChain] {((type == AccountType.Server_Public || type == AccountType.Server_Private) ? "서버" : $"UID {uid}")} 계정 로드 실패: {ex.Message}");
                return CreateAccount(type, password, uid);
            }
        }

    }

    class AccountPassword { public string password { get; set; } }
}
