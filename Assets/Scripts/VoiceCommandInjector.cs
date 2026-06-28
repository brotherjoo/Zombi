using UnityEngine;
using System.Collections;
using Newtonsoft.Json.Linq;
using Vosk;

public class VoiceCommandInjector : MonoBehaviour
{
    [Header("Vosk 설정")]
    public string modelFolderName = "vosk-model-small-ko-0.22";

    [Header("VAD 설정")]
    [Tooltip("이 볼륨 이상이면 발사")]
    public float vadThreshold = 0.02f;
    [Tooltip("침묵 지속 시간 (초)")]
    public float silenceDuration = 0.05f;

    [Header("UI (선택)")]
    public UnityEngine.UI.Text debugText;

    private PlayerInput playerInput;
    private VoskRecognizer recognizer;
    private AudioClip micClip;
    private string micDevice;
    private const int SampleRate = 16000;

    public bool IsSpeaking { get; private set; } = false;
    private float silenceTimer = 0f;

    private void Start()
    {
        playerInput = GetComponent<PlayerInput>();

        if (Microphone.devices.Length == 0) {
            Debug.LogWarning("[Voice] 마이크를 찾을 수 없습니다.");
            enabled = false;
            return;
        }

        string modelPath = System.IO.Path.Combine(
            Application.streamingAssetsPath, modelFolderName);

        Vosk.Model model = new Vosk.Model(modelPath);
        recognizer = new VoskRecognizer(model, SampleRate,
            "[\"재장전\", \"장전\", \"[unk]\"]");

        micDevice = Microphone.devices[0];
        micClip   = Microphone.Start(micDevice, true, 1, SampleRate);

        StartCoroutine(RecognitionLoop());
        Debug.Log("[Voice] 음성인식 시작 — " + micDevice);
    }
    private void Update()
{
    // IsSpeaking 상태면 매 프레임 voiceFire 유지
    if (IsSpeaking)
    {
        playerInput.voiceFire = true;
    }
}

    private IEnumerator RecognitionLoop()
    {
        int lastSample = 0;
        int chunkSize  = SampleRate / 10;
        float[] floatBuf = new float[chunkSize];
        byte[]  byteBuf  = new byte[chunkSize * 2];

        while (true)
        {
            yield return new WaitForSeconds(0.1f);

            int cur = Microphone.GetPosition(micDevice);
            if (cur < lastSample) lastSample = 0;
            if (cur - lastSample < chunkSize) continue;

            micClip.GetData(floatBuf, lastSample);
            lastSample = cur;

            // ─── VAD → 발사 ──────────────────────────────────────
            UpdateVAD(floatBuf);

            // ─── Vosk Final Result → 재장전 ───────────────────────
            for (int i = 0; i < chunkSize; i++)
            {
                short s = (short)(floatBuf[i] * 32767f);
                byteBuf[i * 2]     = (byte)(s & 0xFF);
                byteBuf[i * 2 + 1] = (byte)(s >> 8);
            }

            if (recognizer.AcceptWaveform(byteBuf, byteBuf.Length))
            {
                string text = "";
                try { text = (string)JObject.Parse(recognizer.Result())["text"] ?? ""; }
                catch { continue; }

                if (string.IsNullOrWhiteSpace(text)) continue;

                Debug.Log("[Voice] 인식: " + text);
                if (debugText != null) debugText.text = "🎤 " + text;

                if (Contains(text, "재장전", "장전"))
                {
                    playerInput.voiceReload = true;
                    // 장전 음성이었으면 발사 상태도 강제 종료
                    IsSpeaking = false;
                    playerInput.voiceFire = false;
                    Debug.Log("[Voice] → 재장전");
                }
            }
        }
    }

    private void UpdateVAD(float[] samples)
    {
        float rms = 0f;
        for (int i = 0; i < samples.Length; i++)
            rms += samples[i] * samples[i];
        rms = Mathf.Sqrt(rms / samples.Length);

        if (rms >= vadThreshold)
        {
            silenceTimer = 0f;
            if (!IsSpeaking)
            {
                IsSpeaking = true;
                playerInput.voiceFire = true;
                Debug.Log("[Voice] → 발사");
            }
        }
        else
        {
            if (IsSpeaking)
            {
                silenceTimer += 0.1f;
                if (silenceTimer >= silenceDuration)
                {
                    silenceTimer = 0f;
                    IsSpeaking = false;
                    playerInput.voiceFire = false;
                }
            }
        }
    }

    private bool Contains(string text, params string[] keywords)
    {
        foreach (var kw in keywords)
            if (text.Contains(kw)) return true;
        return false;
    }

    private void OnDestroy()
    {
        Microphone.End(micDevice);
        recognizer?.Dispose();
    }
}