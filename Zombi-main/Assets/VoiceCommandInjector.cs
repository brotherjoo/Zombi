using UnityEngine;
using System.Collections;
using Newtonsoft.Json.Linq;
using Vosk;

public class VoiceCommandInjector : MonoBehaviour
{
    [Header("Vosk 설정")]
    public string modelFolderName = "vosk-model-small-ko-0.22";

    [Header("이동 지속 시간 (초)")]
    public float moveDuration = 0.5f;

    [Header("UI (선택)")]
    public UnityEngine.UI.Text debugText;

    private PlayerInput playerInput;
    private VoskRecognizer recognizer;
    private AudioClip micClip;
    private string micDevice;
    private const int SampleRate = 16000;

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
    		"[\"발사\", \"재장전\", \"[unk]\"]");
        micDevice  = Microphone.devices[0];
        micClip    = Microphone.Start(micDevice, true, 1, SampleRate);

        StartCoroutine(RecognitionLoop());
        Debug.Log("[Voice] 음성인식 시작 — " + micDevice);
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

            for (int i = 0; i < chunkSize; i++) {
                short s = (short)(floatBuf[i] * 32767f);
                byteBuf[i * 2]     = (byte)(s & 0xff);
                byteBuf[i * 2 + 1] = (byte)(s >> 8);
            }

            if (recognizer.AcceptWaveform(byteBuf, byteBuf.Length))
            {
                string json = recognizer.Result();
                HandleResult(json);
            }
        }
    }

	private void HandleResult(string json)
	{
    	string text = "";
    	try { text = (string)JObject.Parse(json)["text"] ?? ""; }
    	catch { return; }

    	if (string.IsNullOrWhiteSpace(text)) return;

    	if (debugText != null) debugText.text = "🎤 " + text;
    	Debug.Log("[Voice] 인식: " + text);

    	if      (Contains(text, "발사", "쏴", "쏘"))
    	    playerInput.voiceFire = true;
    	else if (Contains(text, "재장전", "장전"))
        	playerInput.voiceReload = true;
	}
    	private IEnumerator InjectMove(float value)
    	{
        	float elapsed = 0f;
        	while (elapsed < moveDuration) {
            	playerInput.voiceMove = value;
            	elapsed += Time.deltaTime;
            	yield return null;
        	}
        	playerInput.voiceMove = 0f;
    	}

    private IEnumerator InjectRotate(float value)
    {
        float elapsed = 0f;
        while (elapsed < moveDuration) {
            playerInput.voiceRotate = value;
            elapsed += Time.deltaTime;
            yield return null;
        }
        playerInput.voiceRotate = 0f;
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