# 改善网络的NAT类型

## 背景

去年的“吃鸡”游戏让不少玩家都有了一个Steam账号，但作为一个合格的硬核玩家，Steam游戏库里想必不止有吃鸡一款游戏。

随着Steam、Origin、PlayU（滑稽）等平台的普及，越来越多优秀的大作逐渐进入国内玩家视野。比如GTA5、荣耀战魂、刺客信条等。本文讨论的，就是在开发网游加速器项目时，解决这类P2P游戏的联机过程中遇到的NAT问题。虽然已经离开原项目组，但解决NAT问题的过程还是十分有趣、值得记录的。

## 痛点

作为游戏开发商，持续运营一款游戏是要消耗不小成本的，除去登陆和商城，流量最密集的部分就是游玩过程了。如果一场战局里，服务器只负责把玩家匹配到一个房间，战局开始之后，玩家之间互相连接，那不就能省下买服务器的钱了吗？

于是……

![GTA5-NATSTRICT](https://ws2.sinaimg.cn/large/006tNbRwgy1fxpwpb338xj30k00bmgmc.jpg)

![SPLATOONDISCONNECT](https://ws3.sinaimg.cn/large/006tNbRwgy1fxpwpdh7rzj30zk0k0q6a.jpg)

![PS4NAT](https://ws2.sinaimg.cn/large/006tNbRwgy1fxpwpfswqxj30j60asjrr.jpg)

拜育碧、R星所赐，让我们不得不了解到NAT、DMZ、uPnP的概念，让我们一次又一次充当Stun客户端的测试人员。一番折腾下来，如果屏幕上仍旧显示“NAT严格”，那么我们面对的将是空空荡荡的训练场，怎么也合流不到一个房间的好友，和八国语言所描写的“Internet Connection Lost”。

## 理论分析

![nat-router](https://ws3.sinaimg.cn/large/006tNbRwgy1fxpwphs4hlj30b4042aa1.jpg)

NAT已经是个老生常谈的问题，需要它的原因有很多，比如IPv4地址枯竭、保护内网电脑不被侵入、实现带宽分享等。

如图所示，NAT网关左侧是内网环境，右侧是公网环境。

你的电脑位于内网环境，你的IP地址只有处于同一个子网内的电脑才认得，要把网络包发到公网环境下的服务器，你就需要一个公网IP和端口；同样的，游戏服务器要把包回给你，你同样需要一个公网IP和端口来接收它。

嗯，没错，NAT网关就是给你分配公网IP和端口用的。

![LocalIP](https://ws4.sinaimg.cn/large/006tNbRwgy1fxpwpja3ddj30u00xvjt7.jpg)

嗯，现在子网里有n台电脑，每台电脑访问外网都需要一个公网IP和端口。就像茴字有四种写法一样，给这些电脑分配IP/Port的方式也有四种：

1. 静态分配，每个内网IP都能映射到一个互不重复的公网IP，端口则直接服用TCP/UDP包的源端口。嗯，你想得美……
2. 完全圆锥NAT，一般NAT网关会复用少量几个公网IP，把源端口映射成互不重复的本地端口，以此区分不同的socket连接。然后在该IP/Port上，把包送出去。如下图所示，在该端口上连接多个目标，画成图就像个锥子一样，所以得名“完全圆锥NAT”。这种类型的NAT，就是游戏上显示的“NAT开放”。![FULLCONENAT](https://ws3.sinaimg.cn/large/006tNbRwgy1fxpwppm6cdj30m80a0mxb.jpg)
3. 受限圆锥NAT，在Client主动与Server1建立连接的过程中，NAT网关为Client分配了IP和端口，Server2得知了该IP和端口之后，直接往该端口发包，NAT网关会直接做丢弃处理，不会转发给Client。这种类型的NAT，圆锥结构是受限的，Client必须主动给Server发包，Server才能在该端口上把包回给Client。![RESCONNAT](https://ws3.sinaimg.cn/large/006tNbRwgy1fxpwpt1h2ij30m80a0t8w.jpg)
4. 端口受限圆锥NAT，在受限圆锥的基础上，对Server端口也做了限制。如果Server更换本地端口，继续往NAT网关的IP和给Client分配的端口上发包，网关也会做丢弃处理。这种类型的NAT，既不承认陌生IP，也不承认熟悉IP的陌生端口。![PORTRESCONNAT](https://ws4.sinaimg.cn/large/006tNbRwgy1fxpwpvf61yj30m80a00sw.jpg)
5. 对称NAT，以上几种NAT类型，Client不管连接多少个不同的Server，网关都会分配同一个端口。如果网关为Client所连接的每个不同Server都分配不同端口的话，那么“对称NAT”就诞生了。这种类型的NAT，也就是游戏中的“NAT严格”。之所以说是严格，是因为Server2能从Server1那里得知Client的外网IP和端口，但却无法与之建立连接。而Server2，在游戏中的实际身份，就可以是与你匹配的玩家。![SYMNAT](https://ws1.sinaimg.cn/large/006tNbRwgy1fxpwpynn89j30m80a0mxe.jpg)

我们把NAT类型从“好”到“坏”做个分级：NAT开放（完全圆锥NAT）、NAT中等（受限圆锥NAT、端口受限圆锥NAT）、NAT严格（对称NAT）。这就是游戏里探测到的NAT类型。

不难发现，一条线路所能达到的**<u>NAT等级上限</u>**，是**<u>由这条线路的出口决定</u>**的。

如果你家的宽带出口处是受限圆锥NAT（NAT中等），但游戏内检测却是对称NAT（NAT严格）的话，可以试着开启路由器的DMZ功能、uPnP功能、端口映射功能（后面会提到），这样游戏内会从NAT严格提升到NAT中等，**<u>但是</u>**，再怎么做，也变不成完全圆锥NAT（NAT开放）。原因就是你家宽带的出口处的NAT网关，是做成受限圆锥NAT的。在不改变流量出口的前提下，这就是你家网络所能达到的最好的NAT类型了。

## 加速器原来的代理架构

我们的网游加速器有两个模式，以模式二（即LSP模式）为例，它的代理架构（发包过程）是这样的。

> **Client -> LB VIP -> Socks5 Server -> Game Server**
>
> Client：游戏客户端
>
> LB VIP：虚拟IP，由腾讯云提供的负载均衡功能，绑定的接入机器位于国内（华北、华南、华东、西南）
>
> Socks5 Server：S5代理服务器，LSP直接交互的目标，部署在海外（香港、欧服、美服、日韩、东南亚等）。从LB到S5走的是专线。
>
> Game Server：游戏服务器

发包过程完成后，S5 Server会将这个连接的四元组记录到一张表里：

> **Source IP/Port <-> Target IP/Port**

这样，当目标服务器回包过来时，S5才能查询Target IP/Port，找到Source IP/Port，把网络包回给源客户端。

不难发现：

1. 经过代理加速后，S5 Server变成了流量出口
2. Client和接入服务器之间，是正常的C/S连接，不受NAT类型的影响
3. S5 Server处理目标服务器回包的方式（也就是这张四元组的表结构），直接影响加速后的NAT类型
4. S5 Server其实充当的就是裸连时NAT网关的角色

那么，这样的代理架构，会导致怎样的NAT类型呢？

## 游戏的通信过程

卖个关子，我们来看一下玩家之间P2P连接的游戏，通信过程是怎样的。下面是最简单的、没有主动打洞（后面会提到）行为的P2P游戏的通信过程。

> **A [IP A, Port A] --register-> Game Server**
>
> **B [IP B, Port B] --register-> Game Server**
>
> **A --query B-> Game Server <-query A-- B**
>
> **B -> A [IP A, Port A]**
>
> A：玩家A
>
> B：玩家B
>
> IP A, Port A：玩家A的出口IP/Port
>
> IP B, Port B：玩家B的出口IP/Port
>
> register：玩家登陆游戏，服务器记下玩家名字和他的IP/Port
>
> query：玩家向服务器查询同一房间内的另一个玩家的IP/Port

简单地说，就是玩家A和玩家B都登陆游戏，服务器分别记下A和B的IP/Port，然后将A和B匹配到同一房间，B向服务器询问A的IP地址，然后B使用IP B, Port B向IP A, Port A发包。

假设玩家A处于NAT网络中，玩家B处于公网环境中，那么A能不能收到B的包，取决于A的NAT网络类型：

- 完全圆锥NAT（NAT开放）：A能收到B的包
- 受限圆锥NAT（NAT中等）：A收不到B的包
- 端口受限圆锥NAT（NAT中等）：A收不到B的包
- 对称NAT（NAT严格）：A收不到B的包

但是，在B给A发包之前，我们增加一个动作：让A先给B（IP B, Port B）发包，那么根据A的NAT类型，结果又会有变化：

- 完全圆锥NAT（NAT开放）：A能收到B的包
- 受限圆锥NAT（NAT中等）：A能收到B的包
- 端口受限圆锥NAT（NAT中等）：A能收到B的包
- 对称NAT（NAT严格）：A依然收不到B的包

可以看到，中间两种同属“中等”的NAT类型里，A都能收到B的包，他俩变得都能正常建立连接了。而我们新增的 让A先给B发包 的动作，就叫“打洞”。

如果任何时候中间这两种NAT类型表现都一致的话，我们就不必取两个名字了。那么它们在什么时候会有不同的表现呢？

我们修改一下B给A发包的动作，上面的流程里，A先给B发包，接着在B给A发包时，B使用的是跟它给服务器发包时一样的端口 Port B，这一次，我们让B使用另一个端口 Port B2 给A（IP A, Port A）发包。这时，根据A的NAT类型，结果就发生了变化：

- 完全圆锥NAT（NAT开放）：A能收到B的包
- 受限圆锥NAT（NAT中等）：A能收到B的包
- 端口受限圆锥NAT（NAT中等）：A收不到B的包
- 对称NAT（NAT严格）：A依然收不到B的包

聪明的你，肯定明白了为什么下面这种NAT类型，要叫做**端口**受限圆锥NAT了。

事实上，多数游戏在等待其他玩家连接时，都会主动去做这个“打洞”的动作，说白了就是提前给另一个玩家发包，好让NAT网关记住这个连接，放行其他玩家发来的包。也就是说，只要你的网络是“NAT中等”的，那么就可以正常与其他玩家联机比赛，只不过匹配过程受“打洞”的影响，匹配速度会比“NAT开放”的情况慢一些。

让我们做个小测验：

1. 为什么对称NAT无法与其他玩家联机？作为游戏开发者，如果硬要兼容对称NAT类型的话，在“打洞”的基础上，还要做哪些操作？
2. 刚刚的例子里，B是位于公网环境的，B不会有NAT问题，但如果B也是NAT中等，那么A和B能建立连接吗？换言之，NAT中等的话，真的只是匹配速度稍受影响而已吗？
3. 回想一下P2P游戏匹配玩家的过程，如果你家网络出口处的NAT网关是受限圆锥NAT的话，你能想到哪些能在路由器上做的设置或者可用的功能，能让客户端的NAT类型下降成对称NAT？

如果这三个问题都没有思路，建议查阅一下相关资料再继续阅读。

![Lizi](https://ws3.sinaimg.cn/large/006tNbRwgy1fxpwq2vmbyj307106twfg.jpg)

假设游戏场景是一个大操场，操场上有数不清的玩家：

1. NAT开放的玩家：玩家登陆游戏后，能在操场上看到无数的玩家在行走、奔跑、和一些不可描述的事情，宛若白昼
2. NAT中等的玩家：玩家登陆游戏后，首先是一片黑暗、空无一人的操场，玩家拿着手电筒，手电筒发出光线（主动发包）才能看到人，于是慢慢的他看到了操场上有10个人、20个人、30个人…最后剩下一些NAT严格的玩家，他怎么都看不见
3. NAT严格的玩家：玩家登陆游戏后，同样是一片黑暗、空无一人的操场，玩家拿的手电筒是坏的（主动发包给其他玩家，但NAT网关分配了不同端口，别的玩家收到包并不认识这个地址，除非包体内容能证明玩家身份，这就依赖游戏实现了）

这个例子不是很严谨，但可以大致说明NAT开放的线路，对P2P游戏的重要性。

## 分析加速器原来的代理架构

回到刚才卖的关子，在代理架构中，S5服务器扮演着NAT网关的角色。它在处理回包时，是依靠查表实现的：

> **Source IP/Port <-> Target IP/Port**

如果Target IP/Port都不在表里，S5服务器就不知道该把包回给谁，相应的客户端也就收不到回包了。这样的表结构，对应的NAT类型是端口受限圆锥NAT。

还有一点需要说明，S5服务器是在7777端口上接收UDP包，转发时会单独开辟一个新的本地端口。如果Client经过S5分别往两个不同的服务器发送UDP包，那么S5会为这两个连接分配不同的本地端口。这样的转发逻辑，决定了S5服务器实际的NAT类型是对称NAT。

在绝地求生的加速线路上运行NAT探测程序，与实验结果相符：

![PUBGLineNATStrict](https://ws4.sinaimg.cn/large/006tNbRwgy1fxpwq66o60j31el0u0tpm.jpg)

截图里是一个NAT探测程序（后面会提到）（后面好像要提很多东西的样子，我他喵好虚啊，这坑到底能不能填上=。=），从日志中可以看到，客户端在分别连接193.112.152.178:7777 和 139.199.88.43:12780 这两台服务器时，S5服务器分别分配了两个不同的端口 47049 和 55332，也就是对称型NAT。

## 改进加速器的代理结构

像GTA5、幽灵行动这样的游戏，是需要NAT开放的加速线路，才能获得最佳的联机体验的。综合上面提到的两点，要想把S5服务器做成完全圆锥NAT（NAT开放），需要修改这个表结构：

> **Source IP/Port <--Local Port--> Target * **
>
> Local Port：S5服务器为该玩家（来源socket）分配的本地端口
>
> Target *：表示任意目标

大体是这个样子：

![NATProto](https://ws3.sinaimg.cn/large/006tNbRwgy1fxpwq8xzq9j30hu0bv74a.jpg)

玩家A连接Server时S5服务器分配了本地端口a，那么玩家B通过S5的IP / 端口a发包，S5服务器会把在端口a上收到的包，都转发回玩家A。

这种改动的原理，用一句话说明，就是给了玩家一个公网IP，这个端口从现在起就是你的了~~ 带来的风险，就是将玩家暴露在了公网环境下，不分来源地将发到该端口的包都转发回玩家。

相信聪明的你，也能体会到NAT网关的良苦用心，为什么要做受限圆锥？因为网关无法相信玩家B是从正规渠道获取到的玩家A的出口地址的，玩家B对NAT网关来说，就是一台不可信任的安全性未知的来源。所以网关要求，除非A主动给B发包（说明B是A的朋友，而不是陌生人），否则会拒掉B首先发过来的数据包。

## 曲折：NAT线路遇到的部署问题

新版的S5程序，部署在了P2P游戏的线路上，期待将NAT等级提升一个档次。不过上线之后，运行探测程序，给出的结果却是端口受限圆锥NAT（NAT中等）。如图所示：

![CustomNATMiddle](https://ws2.sinaimg.cn/large/006tNbRwgy1fxpwqbeqvjj31900u0k5y.jpg)

也就是说，从表现上来看，S5服务器是不信任未知来源的回包的。并且不仅是受限圆锥，也做了端口限定。

（仍然以NAT玩家A、公网玩家B和服务器为例）通过在S5服务器上抓包排查，预期能收到3个来自玩家B的数据包，却只收到了1个。为什么期待3个呢？这个我们后边会说（这篇文章已然变成车祸现场）。

于是查了一下S5服务器的安全组，它的入站规则是这样的：

![S5FWBefore](https://ws4.sinaimg.cn/large/006tNbRwgy1fxpwqdzj4oj31bq0ggwg3.jpg)

其中7777-7780是代理转发使用的UDP和TCP端口，20005和7781分别是后续扩展的专用端口。这套规则，对除此之外的端口上收到的包，全部Drop。

（事实上，还有一条约定俗成的入站规则，那就是如果在这个端口上发过包，那么入站规则对该端口便是放行的。所以S5经由本地端口转发的连接，才能正常收发包）

所以这样的入站规则决定了，NAT类型注定是受限圆锥的 & 端口限定的，也就是端口受限圆锥NAT。

经过运维同学的考量，将入站规则逐步放宽，仅针对NAT的线路放开随机端口的入站限制，调整之后，探测程序终于给出结果，线路变成了NAT开放。

![ClientA](https://ws2.sinaimg.cn/large/006tNbRwgy1fxpwqg16nfj30wl0u0tgb.jpg)

可喜可贺，可口可乐！

## 怎样探测网络的NAT类型

前面讲到P2P游戏的通信过程时，其实就已经说明了NAT的探测过程（玩家A处于NAT环境，玩家B处于公网环境）。我们假定玩家A已经知道自己位于NAT环境下（比较出口IP和内网IP地址即可），然后执行下面的流程：

> // 步骤1 玩家A和玩家B向户口服务器登记身份
>
> **A [IP A, Port A] --register-> Game Server**
>
> **B [IP B, Port B] --register-> Game Server**
>
> // 步骤2 玩家A和玩家B通过户口服务器，查询对方的IP/Port
>
> **A --query B-> Game Server <-query A-- B**
>
> // 步骤3 玩家B采用随机端口B2给A发包，如果A能收到，则A的NAT类型为完全圆锥NAT，探测结束；如果超时则继续流程
>
> **B [IP B, Port B2] -> A [IP A, Port A]**
>
> // 步骤4 玩家A主动往IP B, Port B发包，玩家B将他看到的A的出口地址IP A, Port A2作为包体内容，回复给玩家A。如果A看到Port A 不等于 Port A2，那么A的类型为对称NAT，探测结束；如果Port A == Port A2，则继续流程
>
> **A [IP A, Port A2] -> B [IP B, Port B]**
>
> // 步骤5 玩家B更换端口B3，给A发包，如果A能收到，则A为受限圆锥NAT；如果超时，则A为端口受限圆锥NAT
>
> **B [IP B, Port B3] -> A [IP A, Port A]**
>
> IP A, Port A：玩家A的出口IP/Port
>
> IP B, Port B：玩家B的出口IP/Port
>
> register：玩家登陆游戏，服务器记下玩家名字和他的IP/Port
>
> query：玩家向服务器查询同一房间内的另一个玩家的IP/Port

在这个过程中，玩家B一共给玩家A发了3个包，分别使用三个不同的端口Port B2、Port B 和 Port B3。其中，Port B充当的是IP回显端口，Port B2 和 Port B3 都是随机端口，用来确定玩家A的网络类型。

刚刚提到，在S5服务器上抓包时，期待收到B的3个包，却只收到了1个，从而断定时防火墙的问题。就是因为玩家A主动联系过玩家B的IP回显端口 Port B，所以IP回显数据包能够抓到，而从随机端口 Port B2 和 Port B3 发来的包，都被防火墙过滤掉了。

让我们做个小测验：

1. 探测程序连续跑两次，两次的结果会不会互相干扰？误导一下，第一次探测时A已经给B打洞了，那么下一次探测，会不会B可以直接把包回给A呢？
2. 送分题，梳理一下，整个探测过程中，A、B、Server分别互相发了多少个数据包，可不可以省掉其中一个？

## 探测程序的开发

好的，又一波小测验做完了，请不要打我 >_<，这些问题在实际操作中真的会遇到，只讲原理的话，很难有切身体会，但是把这些点都想通之后，会对NAT问题有更透彻的理解。

为了在实验室搭建探测NAT类型的环境，我们需要2个公网IP和1个客户端，来充当玩家A & B 和 户口服务器。

这里就不多废话了，这块代码不涉及线上业务，我就放到了 Gitee 上，供大家查阅参考，欢迎拍砖（请原谅一个为了这破事儿不得不学了 Git 的萌新，不得不说，真香），欢迎小窗交流世界上最好的语言 C# 和 世界上最好的终端 Powershell ：

https://gitee.com/nuetrino/CustomNATServerEx.git

户口服务器 和 玩家B 使用 .Net Core 制作，分别部署在两台腾讯云 Cent OS 上（相信我，等我搞清楚怎么配置双网卡的机器之后，一定会把两个程序合到一起 >_<）。玩家A 使用 .Net Framework 制作，这个端运行在需要探测NAT类型的 Windows 机器上。

户口服务器 和 玩家B 部署好之后，在客户端执行 玩家A 即可探测NAT类型。

协议约定：

> 请求命令
> Command:
> register -> 向服务端注册自己的名字和地址关联信息，参数填自己的名字
> queryuser -> 从服务器读取另一个名字的机器的地址信息，参数填另一台机器的名字
> queryall -> 查询服务器上已登记的所有机器的名称，参数不填
> erase -> 从服务器删除一个名字的机器的信息，参数填要删除的机器的名字
> echoip -> IP回显，参数不填
> a2b_r -> （私有命令）命令B客户端给A在随机端口发消息，参数不填

协议报文：

```xml
登记本机，并查询本机的出口IP
<root>
  <requestlist>
    <request command="register" param="A" />
    <request command="queryuser" param="A" />    
  </requestlist>
</root>

擦除指定机器的记录
<root>
  <requestlist>
    <request command="erase" param="A" />
  </requestlist>
</root>

查询指定机器的出口IP
<root>
  <requestlist>
    <request command="queryuser" param="A" />
  </requestlist>
</root>

获取所有机器的名称
<root>
  <requestlist>
    <request command="queryall" param="" />
  </requestlist>
</root>

查询自己的出口IP
<root>
  <requestlist>
    <request command="echoip" param="" />
  </requestlist>
</root>
```

应答报文同样为一个responselist，每条request按顺序对应一条response。

最后，强烈安利 RoslynPad，相信 C# 的同好肯定知道神器 LINQPad，但是前者的出现让我打消了购买后者专业版的想法。

![RoslynPad](https://ws1.sinaimg.cn/large/006tNbRwgy1fxpwqjso1uj31do0u00v3.jpg)

*图例：随手写写画画，利用socket连接时的行为获取本地IP地址*

## One more thing

如果你真的超级有耐心，读完了上面我胡说八道的所有东西，那一定记得 Google 一下这个东西：Stun。有惊喜。

希望本文对你深入了解P2P通信原理有所帮助。