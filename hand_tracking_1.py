import cv2
import mediapipe as mp
import time
from mediapipe.tasks import python
from mediapipe.tasks.python import vision

# Path to model
model_path = "/Users/student/PycharmProjects/HandTracking/hand_landmarker.task"

# Setup Tasks API (this is the tasks bit cause solutions died)
BaseOptions = python.BaseOptions
HandLandmarker = vision.HandLandmarker
HandLandmarkerOptions = vision.HandLandmarkerOptions
VisionRunningMode = vision.RunningMode

#within handmarker we are setting some options - telling it where the hand tracking code is stored,
## the fact were in VIDEO and number of hands
options = HandLandmarkerOptions(
    base_options=BaseOptions(model_asset_path=model_path),
    running_mode=VisionRunningMode.VIDEO,
    num_hands=2
)

#this is you actually making the detector
landmarker = HandLandmarker.create_from_options(options)

# ---- Manually define hand connections (21 landmarks, see hand landmarks mediapipe guide) ----
HAND_CONNECTIONS = [
    # Thumb
    (0,1), (1,2), (2,3), (3,4),
    # Index
    (0,5), (5,6), (6,7), (7,8),
    # Middle
    (5,9), (9,10), (10,11), (11,12),
    # Ring
    (9,13), (13,14), (14,15), (15,16),
    # Pinky
    (13,17), (17,18), (18,19), (19,20),
    # Wrist
    (0,17)
]

# Open webcam - THIS IS WHERE WEVE SET IT AS VIDEO
webcam = cv2.VideoCapture(0)

while webcam.isOpened():
    success, frame = webcam.read()
    if not success:
        break

    #converting from bgr to rgb because the hand tracking reads rgb but cv2 send bgr
    rgb_frame = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
    mp_image = mp.Image(image_format=mp.ImageFormat.SRGB, data=rgb_frame)

    #using timestamp to get the detection result
    timestamp = int(time.time() * 1000)
    detection_result = landmarker.detect_for_video(mp_image, timestamp)

    h, w, _ = frame.shape

    if detection_result.hand_landmarks:
        for hand_landmarks in detection_result.hand_landmarks:

            # Draw landmarks
            for landmark in hand_landmarks:
                x = int(landmark.x * w)
                y = int(landmark.y * h)
                cv2.circle(frame, (x, y), 5, (0, 255, 0), -1)

            # Draw connections
            for connection in HAND_CONNECTIONS:
                start_idx, end_idx = connection

                x0 = int(hand_landmarks[start_idx].x * w)
                y0 = int(hand_landmarks[start_idx].y * h)

                x1 = int(hand_landmarks[end_idx].x * w)
                y1 = int(hand_landmarks[end_idx].y * h)

                cv2.line(frame, (x0, y0), (x1, y1), (255, 0, 0), 2)

    cv2.imshow("Astor", frame)

    # quit the program is you press q
    if cv2.waitKey(5) & 0xFF == ord("q"):
        break

webcam.release()
cv2.destroyAllWindows()
