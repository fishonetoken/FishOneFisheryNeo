using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using Neo.SmartContract.Framework.Services.System;
using Helper = Neo.SmartContract.Framework.Helper;

using System;
using System.ComponentModel;
using System.Numerics;


namespace NFTContract
{
    /**
     * smart contract for Gladiator
     * @author Clyde
     */
    public class NFT : SmartContract
    {
        // global storage
        // "totalSupply" :  total count of NFT minted
        // "tokenURI" : URI base of NFT // optional

        // tokens :         map<tokenid:biginteger, info:NFTInfo>           // NFT ID to NFTInfo, key = tokenid
        // approve :        map<tokenid:biginteger, address:hash160>        // NFT ID to address which the token is approved, key = "apr"+tokenid

        // use byte\ushort\uint\ulong as index depends on total NFT amount
        // index :          map<index:uint, tokenid:biginteger>             // NFT index to NFT ID, key = "idx"+index, if tokenid is same to index, this can be ignore, optional
        // owner index :    map<address:hash160, tokens:uint[]>             // Owner Address to tokens index array, key = "own"+address, optional
        // "auction": addr          // the auction addr
        // "attrConfig" : AttrConfig // the config of attr

        // extra data:      map<extradatakey:string, data:byte[]>           // NFT ID + datakey to extra data, key = "ex"+tokenid+datakey, optional
        // broker :         map<owner:hash160, broker:hash160>              // Owner Address to Broker Address which is approved, optional

        /**
         * 渔场属性结构数据
         */
        [Serializable]
        public class NFTInfo
        {
            
            public byte[] owner; // 渔场拥有者

            public int isBinded; //bool 是否绑定
            public BigInteger bindId; //uint256	绑定地址
            public BigInteger Id; //uint256	渔场id

        }

        

        /**
         * 渔场交易记录
         */
        public class TransferInfo
        {
            public byte[] from;
            public byte[] to;
            public BigInteger value;
        }

        // notify 转账通知
        public delegate void deleTransfer(byte[] from, byte[] to, BigInteger value);
        [DisplayName("transfer")]
        public static event deleTransfer Transferred;

        // notify 授权通知，暂未实现
        public delegate void deleApproved(byte[] owner, byte[] approved, BigInteger tokenId);
        [DisplayName("approve")]
        public static event deleApproved Approved;

        // notify 新的渔场通知
        public delegate void newFOF(BigInteger tokenId, byte[] owner, int isBinded,  
            BigInteger cooldownIndex, BigInteger nextActionAt,
            BigInteger cloneWithId, BigInteger birthTime,);
        [DisplayName("birth")]
        public static event newFOF Birthed;


        // 合约拥有者，超级管理员
        public static readonly byte[] ContractOwner = "AHaAR3QhJtPTvxQfRupigKnbXS3GyH8We8".ToScriptHash();
        // 有权限发布0代角斗士的钱包地址
        public static readonly byte[] MintOwner = "AHaAR3QhJtPTvxQfRupigKnbXS3GyH8We8".ToScriptHash();

        // 名称
        public static string Name() => "FishOneFishery";
        // 符号
        public static string Symbol() => "FOF";

        // 存储已发行的key
        private const string KEY_TOTAL = "totalSupply";
        // 发行总量的key
        private const string KEY_ALL = "allSupply";
        //发行总量
        private const ulong ALL_SUPPLY_CG = 30000;
        //版本
        public static string Version() => "1.0.1";

        /**
         * 获取渔场拥有者
         */
        public static byte[] ownerOf(BigInteger tokenId)
        {
            object[] objInfo = _getNFTInfo(tokenId.AsByteArray());
            NFTInfo info = (NFTInfo)(object) objInfo;
            if (info.owner.Length>0)
            {
                return info.owner;
            }
            else
            {
                //return System.Text.Encoding.ASCII.GetBytes("token does not exist");
                return new byte[] { };
            }
        }

        /**
          * 已经发行的渔场总数
          */
        public static BigInteger totalSupply()
        {
            return Storage.Get(Storage.CurrentContext, KEY_TOTAL).AsBigInteger();
        }

        /**
          * 发行的渔场总数
          */
        public static BigInteger allSupply()
        {
            return Storage.Get(Storage.CurrentContext, KEY_ALL).AsBigInteger();
        }

        /**
         * uri
         */
        public static string tokenURI(BigInteger tokenId)
        {
            return "uri/" + tokenId;
        }

        /**
         * 生成新的渔场数据，生成事件
         */
        private static BigInteger createFOF(byte[] tokenOwner, byte isBinded, BigInteger bindId,
            BigInteger Id      
)
        {
            if (tokenOwner.Length != 20)
            {
                // Owner error.
                return 0;
            }
           
            //
            if (Runtime.CheckWitness(MintOwner))
            {
                //判断下是否超过总量
                byte[] tokenaId = Storage.Get(Storage.CurrentContext, KEY_ALL);
                byte[] tokenId = Storage.Get(Storage.CurrentContext, KEY_TOTAL);
                if (tokenId.AsBigInteger()>= tokenaId.AsBigInteger())
                {
                    return 0;
                }
                BigInteger newToken = tokenId.AsBigInteger() + 1;
                tokenId = newToken.AsByteArray();

                NFTInfo newInfo = new NFTInfo();
                newInfo.owner = tokenOwner;
                newInfo.isBinded = 0;
                newInfo.bindID = 0;
                newInfo.Id = tokenId


                _putNFTInfo(tokenId, newInfo);
                //_addOwnerToken(tokenOwner, tokenId.AsBigInteger());

                Storage.Put(Storage.CurrentContext, KEY_TOTAL, tokenId);

                // notify
                Birthed(tokenId.AsBigInteger(), newInfo.owner, newInfo.isBinded, newInfo.bindID, newInfo.Id);
                return tokenId.AsBigInteger();
            }
            else
            {
                Runtime.Log("Only the contract owner may mint new tokens.");
                return 0;
            }
        }


        /**
         * 将渔场资产转账给其他人
         */
        public static bool transfer(byte[] from, byte[] to, BigInteger tokenId)
        {
            if (from.Length != 20|| to.Length != 20)
            {
                return false;
            }

            StorageContext ctx = Storage.CurrentContext;

            if (from == to)
            {
                //Runtime.Log("Transfer to self!");
                return true;
            }

            object[] objInfo = _getNFTInfo(tokenId.AsByteArray());
            if(objInfo.Length == 0)
            {
                return false;
            }

            NFTInfo info = (NFTInfo)(object)objInfo;
            byte[] ownedBy = info.owner;

            if (from != ownedBy)
            {
                //Runtime.Log("Token is not owned by tx sender");
                return false;
            }

            info.owner = to;
            _putNFTInfo(tokenId.AsByteArray(), info);

            //remove any existing approvals for this token
            byte[] approvalKey = "apr/".AsByteArray().Concat(tokenId.AsByteArray());
            Storage.Delete(ctx, approvalKey);

            //记录交易信息
            _setTxInfo(from, to, tokenId);

            Transferred(from, to, tokenId);
            return true;

        }


        /**
         * 获取渔场信息
         */
        public static NFTInfo tokenData(BigInteger tokenId)
        {
            object[] objInfo = _getNFTInfo(tokenId.AsByteArray());
            NFTInfo info = (NFTInfo)(object)objInfo;
            return info;
        }

        /**
         * 获取总发行量
         */
        public static byte[] getAllSupply()
        {
            return Storage.Get(Storage.CurrentContext, "auction");
        }

        /**
         * 获取拍卖地址
         */
        public static byte[] getAuctionAddr()
        {
            return Storage.Get(Storage.CurrentContext, "auction");
        }

        /**
         * 设置拍卖地址
         */
        public static bool setAuctionAddr(byte[] auctionAddr)
        {
            if (Runtime.CheckWitness(ContractOwner))
            {
                Storage.Put(Storage.CurrentContext, "auction", auctionAddr);
                Storage.Put(Storage.CurrentContext, KEY_ALL, ALL_SUPPLY_CG);
                return true;
            }
            return false;
        }


        /**
         * 合约入口
         */
        public static Object Main(string operation, params object[] args)
        {
            if (Runtime.Trigger == TriggerType.Verification)
            {
                if (ContractOwner.Length == 20)
                {
                    // if param ContractOwner is script hash
                    //return Runtime.CheckWitness(ContractOwner);
                    return false;
                }
                else if (ContractOwner.Length == 33)
                {
                    // if param ContractOwner is public key
                    byte[] signature = operation.AsByteArray();
                    return VerifySignature(signature, ContractOwner);
                }
            }
            else if (Runtime.Trigger == TriggerType.VerificationR)
            {
                return true;
            }
            else if (Runtime.Trigger == TriggerType.Application)
            {
                //必须在入口函数取得callscript，调用脚本的函数，也会导致执行栈变化，再取callscript就晚了
                var callscript = ExecutionEngine.CallingScriptHash;
                if (operation == "version") return Version();
                if (operation == "name") return Name();
                if (operation == "symbol") return Symbol();
                if (operation == "decimals") return 0; // NFT can't divide, decimals allways zero
                if (operation == "totalSupply") return totalSupply();

                if (operation == "hasExtraData") return false;
                if (operation == "isEnumable") return false;
                if (operation == "hasBroker") return false;

                if (operation == "ownerOf")
                {
                    BigInteger tokenId = (BigInteger)args[0];
                    return ownerOf(tokenId);
                }

                if (operation == "transfer")
                {
                    if (args.Length != 3)
                        return false;

                    byte[] from = (byte[])args[0];
                    byte[] to = (byte[])args[1];
                    BigInteger tokenId = (BigInteger)args[2];

                    //没有from签名，不让转
                    if (!Runtime.CheckWitness(from))
                    {
                        return false;
                    }
                    //如果有跳板调用，不让转
                    if (ExecutionEngine.EntryScriptHash.AsBigInteger() != callscript.AsBigInteger())
                    {
                        return false;
                    }
                    return transfer(from, to, tokenId);
                }

                if (operation == "transferFrom_app")
                {
                    if (args.Length != 3)
                        return false;

                    byte[] from = (byte[])args[0];
                    byte[] to = (byte[])args[1];
                    BigInteger tokenId = (BigInteger)args[2];

                    //没有from签名，不让转
                    if (!Runtime.CheckWitness(from))
                    {
                        return false;
                    }
                    byte[] auctionAddr = Storage.Get(Storage.CurrentContext, "auction");
                    if(callscript.AsBigInteger() != auctionAddr.AsBigInteger())
                    {
                        return false;
                    }
                    return transfer(from, to, tokenId);
                }

                if (operation == "transfer_app")
                {
                    if (args.Length != 3)
                        return false;

                    byte[] from = (byte[])args[0];
                    byte[] to = (byte[])args[1];
                    BigInteger tokenId = (BigInteger)args[2];

                    //如果from 不是 传入脚本 不让转
                    if (from.AsBigInteger() != callscript.AsBigInteger())
                        return false;

                    return transfer(from, to, tokenId);
                }


                if (operation == "mintToken")
                {
                    if (args.Length != 34) return 0;
                    byte[] owner = (byte[])args[0];
                    

                    return mintToken(owner, );
                }


                if (operation == "approve")
                {
                    byte[] approved = (byte[])args[0];
                    BigInteger tokenId = (BigInteger)args[1];

                    return approve(approved, tokenId);
                }
                if (operation == "getAuctionAddr")
                {
                    return getAuctionAddr();
                }

                if (operation == "getAllSupply")
                {
                    return getAllSupply();
                }

                if (operation == "setAuctionAddr")
                {
                    if (args.Length != 1) return 0;
                    byte[] addr = (byte[])args[0];

                    return setAuctionAddr(addr);
                }


                if (operation == "transferFrom")
                {
                    if (args.Length != 3)
                        return false;

                    byte[] from = (byte[])args[0];
                    byte[] to = (byte[])args[1];
                    BigInteger tokenId = (BigInteger)args[2];
                    return transferFrom(from, to, tokenId);
                }

                if (operation == "tokenURI")
                {
                    BigInteger tokenId = (BigInteger)args[0];
                    return tokenURI(tokenId);
                }

                if (operation == "balanceOf")
                {
                    if (args.Length != 1) return 0;
                    byte[] owner = (byte[])args[0];
                    return balanceOf(owner);
                }

                if (operation == "tokensOfOwner")
                {
                    byte[] owner = (byte[])args[0];

                    return tokensOfOwner(owner);
                }

                if (operation == "tokenOfOwnerByIndex")
                {
                    byte[] owner = (byte[])args[0];
                    BigInteger index = (BigInteger)args[1];

                    return tokenOfOwnerByIndex(owner, index);
                }

                if (operation == "allowance")
                {
                    BigInteger tokenId = (BigInteger)args[0];

                    return allowance(tokenId);
                }
                if (operation == "upgrade")//合约的升级就是在合约中要添加这段代码来实现
                {
                    //不是管理员 不能操作
                    if (!Runtime.CheckWitness(ContractOwner))
                        return false;

                    if (args.Length != 1 && args.Length != 9)
                        return false;

                    byte[] script = Blockchain.GetContract(ExecutionEngine.ExecutingScriptHash).Script;
                    byte[] new_script = (byte[])args[0];
                    //如果传入的脚本一样 不继续操作
                    if (script == new_script)
                        return false;

                    byte[] parameter_list = new byte[] { 0x07, 0x10 };
                    byte return_type = 0x05;
                    bool need_storage = (bool)(object)05;
                    string name = "NFT";
                    string version = "1.1";
                    string author = "CG";
                    string email = "0";
                    string description = "test";

                    if (args.Length == 9)
                    {
                        parameter_list = (byte[])args[1];
                        return_type = (byte)args[2];
                        need_storage = (bool)args[3];
                        name = (string)args[4];
                        version = (string)args[5];
                        author = (string)args[6];
                        email = (string)args[7];
                        description = (string)args[8];
                    }
                    Contract.Migrate(new_script, parameter_list, return_type, need_storage, name, version, author, email, description);
                    return true;
                }
            }

            return false;
        }

        /**
         * 获取渔场结构
         */
        private static object[] _getNFTInfo(byte[] tokenId)
        {

            byte[] v = Storage.Get(Storage.CurrentContext, tokenId);
            if (v.Length == 0)
                return new object[0];
            return (object[])Helper.Deserialize(v);
            // return Helper.Deserialize(v) as TransferInfo;
        }

        /**
         * 存储渔场信息
         */
        private static void _putNFTInfo(byte[] tokenId, NFTInfo info)
        {
            byte[] nftInfo = Helper.Serialize(info);

            Storage.Put(Storage.CurrentContext, tokenId, nftInfo);
        }

        /**
         * 获取交易信息
         */
        public static object[] getTXInfo(byte[] txid)
        {
            byte[] v = Storage.Get(Storage.CurrentContext, txid);
            if (v.Length == 0)
                return new object[0];

            return (object[])Helper.Deserialize(v);
            // return Helper.Deserialize(v) as TransferInfo;
        }

        /**
         * 存储交易信息
         */
        private static void _setTxInfo(byte[] from, byte[] to, BigInteger value)
        {
            TransferInfo info = new TransferInfo();
            info.from = from;
            info.to = to;
            info.value = value;

            byte[] txinfo = Helper.Serialize(info);

            byte[] txid = (ExecutionEngine.ScriptContainer as Transaction).Hash;

            Storage.Put(Storage.CurrentContext, txid, txinfo);
        }




    }
}
