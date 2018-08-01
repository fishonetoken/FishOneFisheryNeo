 # FishOne 渔场

## 一.  合约说明
FishOne 渔场项目中总共使用两个合约：[sgas](https://github.com/NewEconoLab/neo-ns/tree/master/dapp_sgas)，NFT


其中[sgas](https://github.com/NewEconoLab/neo-ns/tree/master/dapp_sgas)合约为NEL开发的通用合约，

NFT 为 渔场主合约

NFT合约管理角渔场资源，包括渔场的生成，绑定游戏，赠送等功能。 
本项目所用合约均使用C#开发。

### 1.	Sgas.cs
作用：用于gas 和[sgas](https://github.com/NewEconoLab/neo-ns/tree/master/dapp_sgas)之间1：1兑换。
由于合约里直接操作gas比较困难，所以需要玩家先将gas通过[sgas](https://github.com/NewEconoLab/neo-ns/tree/master/dapp_sgas)合约转换为NEP5资产，合约里操控的是[sgas](https://github.com/NewEconoLab/neo-ns/tree/master/dapp_sgas)资产，这样合约写起来会比较方便。

### 2.	NFT.cs
作用：渔场主合约，负责生成新的渔场，并转移给用户。

 
## 二 渔场买卖
渔场买卖暂未包含在合约中，预计第二期实现。

