import serial
import numpy as np
import matplotlib.pyplot as plt
import csv
import time

PORT = "COM5"
BAUD = 115200

ser = serial.Serial(PORT, BAUD, timeout=0)

time.sleep(2)
ser.reset_input_buffer()

print("IMU running...")

file = open("imu_data.csv","w",newline="")
writer = csv.writer(file)

writer.writerow(["time","w","x","y","z"])

start_time = time.time()

WINDOW = 200

quat_data = []
angles = []

# ===== 参数 =====

MAX_JUMP = 2.0
ALPHA = 0.6

# ===== 调试统计 =====

packet_count = 0
last_freq_time = time.time()
imu_freq = 0

last_packet_time = None
latency = 0


def quat_angle(q1,q2):

    dot = abs(np.dot(q1,q2))
    dot = np.clip(dot,-1.0,1.0)

    return 2*np.arccos(dot)


plt.ion()

fig,ax = plt.subplots()

line_w, = ax.plot([],[],label="w")
line_x, = ax.plot([],[],label="x")
line_y, = ax.plot([],[],label="y")
line_z, = ax.plot([],[],label="z")

ax.set_ylim(-1.1,1.1)
ax.legend()

last_plot = time.time()

try:

    while True:

        line = ser.readline()

        if not line:
            time.sleep(0.001)
            continue

        try:

            line = line.decode().strip()

            if not line.startswith("Q:"):
                continue

            line = line[2:]

            w,x,y,z = map(float,line.split(","))

            q = np.array([w,x,y,z])

            # ===== norm check =====

            norm = np.linalg.norm(q)

            if abs(norm - 1) > 0.1:
                continue

            # ===== jump filter =====

            if len(quat_data) > 0:

                prev_q = quat_data[-1]

                angle = quat_angle(prev_q,q)

                if angle > MAX_JUMP:
                    continue

                q = ALPHA * q + (1 - ALPHA) * prev_q
                q = q / np.linalg.norm(q)

            # ===== timing =====

            now = time.time()

            if last_packet_time is not None:
                latency = now - last_packet_time

            last_packet_time = now

            # ===== frequency =====

            packet_count += 1

            if now - last_freq_time >= 1.0:

                imu_freq = packet_count / (now - last_freq_time)

                packet_count = 0
                last_freq_time = now

            # ===== time =====

            t = now - start_time

            writer.writerow([t,q[0],q[1],q[2],q[3]])

            quat_data.append(q)

            if len(quat_data) > WINDOW:
                quat_data.pop(0)

            # ===== stability =====

            if len(quat_data) >= 2:

                angle = quat_angle(quat_data[-2],quat_data[-1])
                angles.append(angle)

                if len(angles) > WINDOW:
                    angles.pop(0)

            stability = np.mean(angles) if angles else 0

            score = max(0,100*(1-stability/0.1))

            # ===== plot =====

            if now - last_plot > 0.03:

                data = np.array(quat_data)

                x_axis = np.arange(len(data))

                line_w.set_data(x_axis,data[:,0])
                line_x.set_data(x_axis,data[:,1])
                line_y.set_data(x_axis,data[:,2])
                line_z.set_data(x_axis,data[:,3])

                ax.set_xlim(0,WINDOW)

                ax.set_title(
                    f"Score {score:.1f} | "
                    f"Freq {imu_freq:.1f} Hz | "
                    f"Latency {latency*1000:.1f} ms"
                )

                plt.pause(0.001)

                last_plot = now

        except:
            continue

except KeyboardInterrupt:

    print("Stopped")

finally:

    file.close()
    ser.close()