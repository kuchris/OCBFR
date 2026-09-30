"""Build embedded UI resources from explicit, reviewed translations."""
import json
from pathlib import Path

root = Path(__file__).resolve().parent.parent
mapping = r'''
寻宝战利品	Treasure loot
共 	Total: 
 次	runs
全部	All
今日	Today
本周	This week
本月	This month
筛选结果 	Filtered: 
 次  ·  本周 	 runs  ·  This week: 
 次  ·  本月 	 runs  ·  This month: 
物品获得统计	Loot totals
仅展示稀有物品	Rare items only
当前筛选范围内暂无战利品记录	No loot records in this date range
物品	Item
累计获得	Total obtained
寻宝记录明细	Run history
最新在前	Newest first
最早在前	Oldest first
时间排序	Sort by time
完成时间	Completed at
副本	Duty
战利品	Loot
南征之章（南岛）	South Horn
北征之章（北岛）	North Horn
蜃景幻界新月岛 南征之章	Occult Crescent: South Horn
蜃景幻界新月岛 北征之章	Occult Crescent: North Horn
南征之章	South Horn
北征之章	North Horn
未检测到获得物品消息	No item acquisition messages detected
自动购买配置	Automatic purchases
搜索商品	Search items
仅显示已启用商品	Enabled items only
购买状态：	Purchase status: 
未检测	Not scanned
触发钱币数量	Currency threshold
启用	Enabled
商品	Item
单价	Unit cost
数量	Quantity
优先级	Priority
唯一物品	Unique item
触发钱币数量不足以兑换此商品	The currency threshold is too low for this item
数值越大，越先购买	Higher priority items are purchased first
已启用 	Enabled: 
 项	 items
无法读取商店目录：	Could not read shop catalog: 
目标副本	Target duty
支持 OCBFR	Support OCBFR
启动插件	Start plugin
紧急停止	Emergency stop
切换至完整界面	Switch to full view
切换至简化界面	Switch to compact view
银箱	Silver coffers
铜箱	Bronze coffers
● 运行中	● Running
○ 已停止	○ Stopped
当前选择模式：	Selected duty: 
当前选择模式 	Selected duty: 
当前任务：	Current task: 
当前任务	Current task
寻宝副本选择	Treasure duty
容量	Capacity
自动流程路线	Automatic route
自动流程	Automatic workflow
01 / 进入	01 / Enter
区域同步完成	Area synchronization complete
02 / 检测	02 / Scan
钱币与宝箱	Currency and coffers
03 / 战斗	03 / Combat
BOCCHI 运行中	BOCCHI running
04 / 寻宝	04 / Treasure
等待宝箱上限	Waiting for full coffers
05 / 重进	05 / Re-enter
自动循环	Automatic loop
当前区域 ID	Current territory ID
区域匹配	Territory matches
区域不匹配	Territory does not match
战斗辅助职业	Combat phantom job
宝箱负载	Coffer count
银 	Silver 
铜 	Bronze 
副本与战斗配置	Duty and combat configuration
副本设置	Duty settings
错误：	Error: 
注意：有些辅助职业的辅助技能可能与魔寻宝 CD 存在冲突，不接受因此所产生问题的反馈。默认选择的辅助白魔法师无此问题	Some phantom job abilities share a cooldown with treasure detection. Phantom White Mage, the default selection, avoids this conflict.
DR 自动丢弃预设	DR discard preset
如需自动丢弃跑刀垃圾，请在此处填写 DR 自动丢弃物品模块的预设名称，留空则不启用	Enter a preset name from the DR automatic discard module to discard unwanted loot. Leave empty to disable.
寻宝模式选择	Treasure mode
XSZ 跑刀	XSZ treasure route
DR 跑刀	DR treasure route
XSZ 跑刀为 XSZToolbox 测试码功能，如果你没有权限则不要选择这个模式。	The XSZ route requires access to the XSZToolbox test feature. Select it only if you have access.
寻宝记录	Treasure records
查看寻宝战利品记录	View treasure loot history
可能会用到的文档	Documentation
点击前往	Open documentation
自动葡挞相关功能不接收任何反馈。	Automatic route features are provided without support.
使用说明	Instructions
1. 本插件功能为高危行为，如介意请勿使用；	1. This plugin automates gameplay and carries risk. Use it only if you accept that risk.
2. 使用本插件的必须条件：	2. Required setup:
   1）启用 BOCCHI 及其配套插件，并且【关闭】自动轮换副本功能；	   1) Enable BOCCHI and its companion plugins. Disable automatic duty rotation.
   2）启用 Daily Routines 插件，并启用下列模块：	   2) Enable Daily Routines and the following modules:
      ① 蜃景幻界新月岛 助手　② 更好的辅助职业列表　③ 辅助职业切换指令	      1) Occult Crescent helper  2) Better phantom job list  3) Phantom job switch command
      ④ 自动任务出发确认　⑤ 即刻退本　⑥ 特殊场景探索进入指令	      4) Automatic duty confirmation  5) Instant duty exit  6) Special exploration entry command
一键开启上述模块	Enable required DR modules
已发送一键开启 Daily Routines 模块指令	Sent commands to enable the required Daily Routines modules
自动前往魔之塔设置	Automatic Forked Tower travel
注意：该项功能只会在蜃景天气出现时停止插件功能前往魔之塔进入区域，不会自动进行魔之塔战斗，后续流程需要手动或者由其他插件接管。	When the required mirage weather appears, this feature pauses farming and moves to the Forked Tower entrance. Tower combat and subsequent steps require manual control or another plugin.
如果你不知道上述是什么意思，则不要开启此功能，也不要就此功能进行任何反馈。	Enable this feature only if you understand the tower entry workflow.
蜃景天气出现时自动前往魔之塔区域	Travel to the Forked Tower during mirage weather
直接开始寻宝流程（测试用）	Start treasure route now (test)
直接开始前往魔之塔流程（测试用）	Start tower travel now (test)
Debug: 强制视为宝箱已满（测试完整流程）	Debug: Treat coffers as full (test complete loop)
Debug: 不退本（只测内环+外环）	Debug: Stay in duty (test inner and outer rings)
测试：立即退本并重新进岛	Test: Leave duty and re-enter now
运行中	Running
已停止	Stopped
停止运行	Stop
开始运行	Start
关闭窗口	Close window
持有：	Owned: 
每次兑换：	Received per purchase: 
 个	 items
当前触发值下上限：	Maximum at current threshold: 
首次进岛	Initial entry
未运行	Not running
打开 OCBFR 设置。	Open OCBFR settings.
启动 OCBFR 自动流程。	Start the OCBFR workflow.
停止 OCBFR 自动流程。	Stop the OCBFR workflow.
等待角色登录	Waiting for login
切换岛屿...	Switching islands...
进入副本...	Entering duty...
重新进入副本...	Re-entering duty...
准备购买...	Preparing purchases...
自动购买未能开始：	Could not start automatic purchases: 
自动战斗中	Automatic combat
战斗中，等待脱战...	In combat; waiting to disengage...
下坐骑...	Dismounting...
等待角色可动后切换自由人...	Waiting until movable before switching to Freelancer...
检测宝箱...	Scanning coffers...
宝箱检测未响应，暂停战斗并等待重试...	No scan response; combat paused while waiting to retry...
准备战斗...	Preparing combat...
准备寻宝...	Preparing treasure route...
准备外环寻宝...	Preparing outer ring...
退出副本...	Leaving duty...
寻宝完成（测试模式：不退本）	Treasure complete (test mode: stay in duty)
寻宝完成，准备重进	Treasure complete; preparing to re-enter
前往大水晶...	Moving to the main aetheryte...
未到达小水晶区域，请检查导航功能	Could not reach the aethernet shard; check navigation
XSZ 寻宝中	XSZ treasure route active
已离岛，等待角色可动后重新进岛...	Left island; waiting until movable before re-entering...
加载副本...	Loading duty...
准备前往魔之塔...	Preparing tower travel...
未到达大水晶区域，请检查导航功能	Could not reach the main aetheryte; check navigation
前往魔之塔...	Moving to the Forked Tower...
未能召唤随机坐骑，请检查坐骑可用性	Could not summon a random mount; check mount availability
未到达魔之塔中转区域，请检查导航功能	Could not reach the tower staging area; check navigation
未到达魔之塔进入区域，请检查导航功能	Could not reach the tower entrance; check navigation
天气已结束，恢复战斗	Weather ended; resuming combat
已到达魔之塔，等待接管	Arrived at the Forked Tower; waiting for handoff
未能下坐骑，无法恢复魔寻宝流程	Could not dismount; cannot resume treasure scanning
坐骑动作未完成，无法恢复魔寻宝流程	Mount action is unfinished; cannot resume treasure scanning
检查钱币...	Checking currency...
前往购买地点...	Moving to the currency vendor...
已离开	Left 
未能到达	Could not reach 
大水晶，跳过本轮购买	 main aetheryte; skipping this purchase cycle
无法读取商店数据，自动购买暂不可用	Could not read shop data; automatic purchases unavailable
自动购买失败：	Automatic purchase failed: 
购买完成，重新进岛...	Purchases complete; re-entering island...
角色已离线，插件已停止	Logged out; plugin stopped
传送至小水晶...	Teleporting to an aethernet shard...
附近有人，等待寻宝	Players nearby; waiting to begin treasure route
等待人物可动...	Waiting until movable...
内环寻宝中	Inner ring treasure route active
外环寻宝中	Outer ring treasure route active
上坐骑...	Mounting...
下坐骑，准备换点...	Dismounting; preparing to change shards...
无可用小水晶，已停止	No available aethernet shard; stopped
小水晶传送失败，请检查传送功能	Shard teleport failed; check teleport functionality
下坐骑失败，已停止	Dismount failed; stopped
已通过手动操作停止	Stopped manually
已紧急停止	Emergency stopped
空闲	Idle
请先完成当前确认操作	Finish the current confirmation first
自动购买功能暂未就绪	Automatic purchases are not ready yet
当前无法开始自动购买	Cannot start automatic purchases in the current state
请靠近钱币商人（15 码内）	Move within 15 yalms of the currency vendor
购买配置无效	Invalid purchase configuration
已取消自动购买	Automatic purchases cancelled
自动购买超过 90 秒未取得进展	No purchase progress for over 90 seconds
购买中，等待过图...	Purchasing; waiting for area transition...
购买中，等待脱战...	Purchasing; waiting to disengage...
玩家不可用	Player unavailable
玩家实体未就绪，已中止自动购买	Player object not ready; automatic purchases aborted
当前状态不允许发送 EventStart，已中止自动购买	Current state does not allow EventStart; automatic purchases aborted
EventStart 发包失败，已中止自动购买	EventStart failed; automatic purchases aborted
未能开始自动购买	Could not start automatic purchases
未能打开购买界面	Could not open purchase window
上一笔购买确认未关闭	Previous purchase confirmation remains open
商店中未找到	Item not found in shop: 
，已停止以避免误购	; stopped to avoid purchasing the wrong item
购买确认超时或库存未变化	Purchase confirmation timed out or inventory did not change
未能确认购买结果	Could not confirm purchase result
已购买：	Purchased: 
自动购买结束，无需兑换	Automatic purchases finished; nothing to exchange
购买收尾中...	Finishing purchases...
继续购买...	Continuing purchases...
上一笔商店事件未完全退出，已停止以避免复用错误的钱币商店	Previous shop event did not close; stopped to avoid using the wrong currency shop
上一笔购买未能正常结束	Previous purchase did not finish correctly
自动购买完成	Automatic purchases complete
未能发送购买请求，已停止以避免重复兑换	Could not send purchase request; stopped to avoid duplicate purchases
未能确认购买数量	Could not confirm purchase quantity
购买：	Purchasing: 
，跳过本轮购买	; skipping this purchase cycle
内环	Inner ring
外环	Outer ring
魔之塔	Forked Tower
十二城邦白银币	Enlightenment silver obols
十二城邦白金币	Enlightenment gold obols
十二城邦银币	Silver obols
十二城邦金币	Gold obols
'''

english = {}
for line in mapping.splitlines():
    if not line:
        continue
    source, translation = line.split('\t', 1)
    english[source] = translation

english[' 次'] = ' runs'
english.update({
    '启用 DR 必要模块': 'Enable required DR modules',
    '控制台': 'WORKSPACE', '总览': 'Overview', '自动购买': 'Purchases',
    '测试工具': 'Test tools', '完整界面': 'Full view', '简化界面': 'Compact view',
    '进入': 'Enter', '检测': 'Scan', '战斗': 'Combat', '寻宝': 'Treasure', '重进': 'Re-enter',
    '南岛不支持魔之塔功能': 'Forked Tower travel is unavailable on South Horn.',
    '测试完成后请紧急停止，恢复正常运行前关闭模拟选项。': 'Emergency-stop after testing. Disable simulation options before normal operation.',
})
traditional = json.loads((root / 'tools/ui-traditional.json').read_text(encoding='utf-8'))
missing = english.keys() - traditional.keys()
extra = traditional.keys() - english.keys()
if missing or extra:
    raise ValueError(f'Traditional Chinese catalog mismatch: missing={sorted(missing)}, extra={sorted(extra)}')
entries = [dict(Source=s, TraditionalChinese=traditional[s], English=e) for s, e in english.items()]
job_names = [('Freelancer', '自由人'), ('White Mage', '白魔法師'), ('Samurai', '武士'), ('Ranger', '遊俠'), ('Monk', '武僧'), ('Berserker', '狂戰士'), ('Knight', '騎士'), ('Chemist', '藥劑師'), ('Cannoneer', '炮擊士'), ('Time Mage', '時魔法師'), ('Geomancer', '風水師'), ('Bard', '吟遊詩人'), ('Dancer', '舞者'), ('Gladiator', '角鬥士'), ('Mystic Knight', '魔法劍士'), ('Thief', '盜賊'), ('Oracle', '預言家'), ('Summoner', '召喚師'), ('Dragoon', '龍騎士'), ('Black Mage', '黑魔法師'), ('Ninja', '忍者'), ('Necromancer', '死靈法師'), ('Red Mage', '赤魔法師'), ('Blue Mage', '青魔法師')]
for name, translated in job_names:
    entries.append(dict(Source='Phantom ' + name, TraditionalChinese='幻境' + translated, English='Phantom ' + name))
destination = root / 'src/NorthIslandChestPlugin/UiTranslations.json'
destination.write_text(json.dumps(entries, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print(f'Wrote {len(entries)} bilingual entries')
