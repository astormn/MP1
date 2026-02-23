This is the code for Major Project 1 

hand_tracking:
(1) First version of hand tracking that works that doesn't use mediapipe solutions. 
It uses the webcam to mark the landmarks using mediapipe and then manually adds the lines connecting the landmarks.

imu_arduino: 
Arduino code that outputs any of quaternion, euler angles, ypr and/or acceleration from the MPU6050 IMU

UDPserver: 
Reads the UDP server that unity sets up and then sets up a UDP socket to send the data from python

Server: 
Unity code that sets up a UDP server, waits for the data from python then plots the positions of the hand landmarks in 3D space
