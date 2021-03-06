﻿/*
   Copyright 2020 Jan Kolkmeier <jankolkmeier@gmail.com>

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
*/
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using ASAPToolkit.Unity.Middleware;
using System.IO;
using System;

namespace ASAPToolkit.Unity.Environment {

    [RequireComponent(typeof(IMiddleware))]
    public class AudioStreamingReceiver : MonoBehaviour, IMiddlewareListener {

        [Tooltip("Only play audio from agents that are active in this scene. Note that this will also disable non-agent audio.")]
        public bool onlyActiveAgentClips = false;

        Dictionary<string, AsapAudioClipSource> clipLib;

        private MiddlewareBase middleware;

        public void OnMessage(MSG msg) {
            StreamingClipJSON audioStreamMsg;
            try {
                audioStreamMsg = JsonUtility.FromJson<StreamingClipJSON>(msg.data);
            } catch (System.Exception e) {
                Debug.LogWarning("Failed to parse incomming StreamingClipJSON to JSON: " + msg.data + "\n\n" + e);
                return;
            }

            //Debug.Log("AUDIO DATA "+audioStreamMsg.msgType+" "+msg.data.Length);

            if (audioStreamMsg.msgType == "DATA") {
                StreamingClipDataJSON audioStreamDataMsg;
                try {
                    audioStreamDataMsg = JsonUtility.FromJson<StreamingClipDataJSON>(msg.data);
                } catch (System.Exception e) {
                    Debug.LogWarning("Failed to parse incomming StreamingClipDataJSON to JSON: " + msg.data + "\n\n" + e);
                    return;
                }

                HandleClipData(audioStreamDataMsg);
            }

            if (audioStreamMsg.msgType == "CTRL") {

                StreamingClipCtrlJSON audioStreamControlMsg;
                try {
                    audioStreamControlMsg = JsonUtility.FromJson<StreamingClipCtrlJSON>(msg.data);
                } catch (System.Exception e) {
                    Debug.LogWarning("Failed to parse incomming StreamingClipCtrlJSON to JSON: " + msg.data + "\n\n" + e);
                    return;
                }

                HandleClipControl(audioStreamControlMsg);

                
            }
        }

        private Dictionary<string, float> debounceRetransmit;
        private float retransmitInterval = 0.2f;
        private void RequestClip(string clipId, int part) {
            if (debounceRetransmit == null) debounceRetransmit = new Dictionary<string, float>();
            string hash = clipId + part;
            if (debounceRetransmit.ContainsKey(hash) && debounceRetransmit[hash] + retransmitInterval > Time.time) {
                return;
            }
            debounceRetransmit[hash] = Time.time;
            StreamingClipRetransmitRequestJSON req = new StreamingClipRetransmitRequestJSON(clipId, part);
            middleware.Send(new MSG(JsonUtility.ToJson(req)));
        }

        private void HandleClipControl(StreamingClipCtrlJSON msg) {
            // if we don't know the clip, the only thing we can do is to request a retransmission.
            if (!clipLib.ContainsKey(msg.clipId)) {
                if (msg.cmd != "stop") { // (but we don't care if it's just a stop command)
                    RequestClip(msg.clipId, 0);
                }
                return;
            }


            if (msg.cmd == "play") {
                // Let's check if we have all parts down the line:
                int firstMissingPart = System.Array.IndexOf(clipLib[msg.clipId].partsReceived, false);
                if (firstMissingPart >= 0) {
                    // And ask the first missing part for retransmission
                    RequestClip(msg.clipId, firstMissingPart);
                }

                clipLib[msg.clipId].Play(msg.floatParam);
            } else if (msg.cmd == "stop") {
                clipLib[msg.clipId].Stop();
                Destroy(clipLib[msg.clipId].audioSource.gameObject);
                clipLib.Remove(msg.clipId);
            } else {
                Debug.LogWarning("Command unknown: " + msg.cmd);
            }
        }


        void HandleClipData(StreamingClipDataJSON msg) {
            if (!clipLib.ContainsKey(msg.clipId)) {
                int lengthSamples = (int) msg.frameLength;
                int channels = msg.audioFormat.channels;
                float frequency = msg.audioFormat.sampleRate;
                AsapAudioClipSource asco = (new GameObject(msg.clipId)).AddComponent<AsapAudioClipSource>();
                asco.transform.SetParent(transform);
                asco.Setup(msg.source, onlyActiveAgentClips, msg.clipId, AudioClip.Create(msg.clipId, lengthSamples, channels, (int)frequency, false), msg.partOffsets.Length);
                clipLib.Add(msg.clipId, asco);
            }

            byte[] audioBuffer = System.Convert.FromBase64String(msg.data);
            float[] samples = new float[audioBuffer.Length/2];
            using (MemoryStream stream = new MemoryStream(audioBuffer)) {
                using (BinaryReader reader = new BinaryReader(stream)) {
                    for (int i = 0; i < samples.Length; i++) {
                        samples[i] = reader.ReadInt16() / 32767f;
                    }
                }
            }
            clipLib[msg.clipId].audioSource.clip.SetData(samples, msg.partOffsets[msg.thisPartIdx] / 2);

            if (clipLib[msg.clipId].partsReceived[msg.thisPartIdx]) {
                Debug.LogWarning("Received duplicate part: "+msg.clipId+" "+msg.thisPartIdx);
            } else { 
                clipLib[msg.clipId].partsReceived[msg.thisPartIdx] = true;
            }
        }

        void InstantiateAudioSource(AudioClip c, string id) {

        }

        void Start() {
            clipLib = new Dictionary<string, AsapAudioClipSource>();
            middleware = GetComponent<MiddlewareBase>();
            if (middleware != null) middleware.Register(this);
        }

    }


    [System.Serializable]
    class StreamingClipJSON {
        public string msgType;
        public string source;
        public string clipId;
    }

    [System.Serializable]
    class StreamingClipRetransmitRequestJSON : StreamingClipJSON {
        public int partIdx;
        public StreamingClipRetransmitRequestJSON(string clipId, int partIdx) {
            this.msgType = "retransmit";
            this.source = "";
            this.clipId = clipId;
            this.partIdx = partIdx;
        }
    }

    [System.Serializable]
    class StreamingClipDataJSON : StreamingClipJSON {
        public long frameLength;
        public StreamingClipDataFormatJSON audioFormat;
        public long totalSize;
        public int thisPartIdx;
        public int[] partOffsets;
        public string data;
    }

    [System.Serializable]
    class StreamingClipDataFormatJSON {
        public string encoding;
        public int channels;
        public int frameSize;
        public float sampleRate;
    }


    [System.Serializable]
    class StreamingClipCtrlJSON : StreamingClipJSON {
        public string cmd;
        public double floatParam;
    }

}