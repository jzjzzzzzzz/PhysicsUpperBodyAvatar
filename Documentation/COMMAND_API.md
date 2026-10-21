# 本地命令接口

接口用于让本机 Python 或其他程序控制 Unity 中的虚拟人。目前只接收仿真目标，不提供串口、PWM 或舵机总线输出。

## 1. 连接约定

| 项目 | 约定 |
|---|---|
| 地址 | `127.0.0.1:8765`，仅监听本机 |
| 协议 | TCP，UTF-8，每条 JSON 以换行 `\n` 结束 |
| 连接生命周期 | 一个连接发送一条请求，读取一条响应后关闭 |
| 正常启动 | 启用监听；多实例只有一个能占用默认端口 |
| `-hdtSelfTest` 启动 | 不启用监听，避免外部指令干扰自动验证 |

请求体应短于 8192 字节。空包或过大的包可能直接关闭连接，不保证返回错误 JSON；客户端读取超时建议不小于 6 秒。

## 2. 动作指令

```json
{"action":"wave","duration":2.5}
```

| 字段 | 类型 | 规则 |
|---|---|---|
| `action` | string | `handshake`、`wave`、`double_thumbs_up`、`idle` |
| `duration` | number，可省略 | `0` 或省略表示默认值；指定值须为 1.2–15 秒 |

`duration` 表示整段动作时间，包括进入、保持/摆动及返回阶段。`idle` 走配置中的回位时长，但提供的 `duration` 仍须通过数值校验。

收到不同动作时，状态机先回位，再执行最新待处理动作；相同动作在未返回阶段不会重新起播。它不是逐条执行的动作队列。动作指令会退出手动舵机目标模式。

典型成功响应：

```json
{"accepted":true,"action":"wave","state":"BlendingIn","error":"","side":"","mode":"","targetValues":[],"hardwareCalibrated":false}
```

`state` 为处理请求时的状态，可能是 `Idle`、`BlendingIn`、`Performing` 或 `Returning`，取决于先前动作和切换阶段。

## 3. 查询 13 路目标

```json
{"action":"servo_state","side":"right"}
```

`side` 必须精确为 `left` 或 `right`。`servo_state`、`servo_pose` 的动作名也使用这里的小写形式。

典型响应节选：

```json
{"accepted":true,"action":"servo_state","side":"right","mode":"GESTURES / 7 ARM + 6 HAND","targetValues":[103.7647,90,93.2143,165.1035,5,90,90,14.4,14.4,14.4,14.4,9,90],"hardwareCalibrated":false}
```

`targetValues` 是当前手动目标，或当前动作经配置编码后的目标。**不是 PhysX 的实测关节角，也不是实物编码器读数**；手动输入限速和物理跟踪都可能使实际姿态滞后。

## 4. 发送 13 路目标

```json
{"action":"servo_pose","side":"right","values":[103.765,90,93.214,165.103,5,90,90,180,0,0,0,0,90]}
```

这个默认模板示例大致保持右臂放松，只将食指收拢。数组顺序固定为：

```text
下标     0   1   2   3   4   5   6   7   8   9   10  11  12
逻辑号   A1  A2  A3  A4  A5  A6  A7  H1  H2  H3  H4  H5  H6
```

- `values` 必须恰好有 13 个有限数值，每个值处于相应配置的输入范围内；默认均为 0–180°。
- 全包验证通过才更新目标；某一路错误不会部分更新其他路。
- 一次只指定一侧；两侧同时控制需分别发送。两次发送不是跨侧原子事务。
- 首次进入手动模式会停止动作轨迹，保持另一侧当前目标；后续输入更新目标，经每通道输入限速后交给物理电机。
- `duration` 不用于这两种舵机指令。手动目标持续有效，直到被后续目标替换、释放、播放动作或诊断复位。

发送以下命令释放左右侧手动覆盖并平滑回位：

```json
{"action":"idle"}
```

## 5. 响应字段与错误

| 字段 | 含义 |
|---|---|
| `accepted` | 请求是否合法且已被控制器接收，不表示动作完成 |
| `action` | 成功时回显动作名 |
| `state` | 处理时的动作状态；手动覆盖可能与 `Idle` 同时存在，需结合 `mode` 看 |
| `side` | 舵机请求对应侧；动作请求为空 |
| `mode` | 舵机请求的控制模式；任一侧有手动覆盖时显示 `SERVO INPUT / SIMULATION ONLY` |
| `targetValues` | 舵机请求的 13 路目标；动作响应为空数组 |
| `hardwareCalibrated` | 配置的标定状态标记，不是运行时的实物连接检测 |
| `error` | 拒绝原因；成功时为空字符串 |

未知动作、非法 JSON、不合规 duration、错误 side、通道数量错误、越界或非有限数值会被拒绝。典型错误响应节选：

```json
{"accepted":false,"error":"Input out of calibrated range at A1"}
```

客户端应以 `accepted` 判断是否成功，不依赖固定状态字符串或错误文本。暂停期间仍可接收目标，恢复后才继续物理步进。

## 6. 命令行与 Python 示例

在工程根目录使用内置脚本：

```bash
python3 Tools/send_command.py handshake
python3 Tools/send_command.py wave --duration 2.5
python3 Tools/send_servo.py --side right
python3 Tools/send_command.py idle
```

下面的标准库示例先保留当前右侧目标，再修改食指；`180` 仅适用于默认、未反向的输入模板。

```python
import json
import socket


def request(command):
    payload = (json.dumps(command, allow_nan=False) + "\n").encode("utf-8")
    with socket.create_connection(("127.0.0.1", 8765), timeout=6) as conn:
        conn.sendall(payload)
        with conn.makefile("rb") as stream:
            reply = json.loads(stream.readline(8193))
    if not reply.get("accepted"):
        raise RuntimeError(reply.get("error", "Command rejected"))
    return reply


state = request({"action": "servo_state", "side": "right"})
values = state["targetValues"]
values[7] = 180  # H1，默认配置下完全弯曲
print(request({"action": "servo_pose", "side": "right", "values": values}))
# 需要回位时：request({"action": "idle"})
```

修改标定后，应读取 `Assets/StreamingAssets/Hardware/ServoRigRight.json` 或左侧对应文件来确定范围和方向，不应继续假设 0–180°。工作原理与参数见 [硬件映射](HARDWARE_MAPPING.md)。
