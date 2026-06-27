using System.Collections;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using Vosk;

public class VoskSpeechRecognizer : MonoBehaviour
{
    [Header("Settings")]
    public string modelPath = "vosk-model-small-ko-0.22";
    public int sampleRate = 16000;

    [Header("VAD Settings")]
    public float vadThreshold = 0.02f;   // 말/침묵 구분 음량 기준
    public float silenceDuration = 0.05f; // 침묵 지속 시간

    public bool IsSpeaking { get; private set; } = false; // 현재 말하는 중 여부
    private float silenceTimer = 0f;     // 침묵 타이머
    private Model model;
    private VoskRecognizer recognizer;
    private AudioClip micClip;
    private bool isListening = false;
    private bool isInitialized = false;
    public bool fire { get; private set; } // 감지된 발사 입력값

    void Start()
    {
        StartCoroutine(InitVoskAsync());
    }

    void Update()
    {
        if (GameManager.instance != null
            && GameManager.instance.isGameover)
        {
            fire = false;
            return;
        }
    }

    IEnumerator InitVoskAsync()
    {
        string fullPath = Path.Combine(Application.streamingAssetsPath, modelPath);

        if (!Directory.Exists(fullPath))
        {
            Debug.LogError($"모델 경로를 찾을 수 없습니다: {fullPath}");
            yield break;
        }

        Debug.Log("Vosk 모델 로딩 중...");

        // Model + VoskRecognizer 모두 백그라운드 스레드에서 생성
        Task loadTask = Task.Run(() =>
        {
            model = new Model(fullPath);
            recognizer = new VoskRecognizer(model, sampleRate);
        });

        yield return new WaitUntil(() => loadTask.IsCompleted);

        if (loadTask.IsFaulted)
        {
            Debug.LogError($"모델 로드 실패: {loadTask.Exception?.Message}");
            yield break;
        }

        isInitialized = true;
        Debug.Log("Vosk 초기화 완료!");
        StartListening();
    }

    public void StartListening()
    {
        if (!isInitialized)
        {
            Debug.LogWarning("아직 초기화 중입니다.");
            return;
        }
        if (isListening) return;

        // 마이크 장치 확인
        if (Microphone.devices.Length == 0)
        {
            Debug.LogError("마이크를 찾을 수 없습니다.");
            return;
        }

        isListening = true;
        micClip = Microphone.Start(null, true, 10, sampleRate);

        // 마이크 시작 대기
        StartCoroutine(WaitForMicAndRecognize());
        Debug.Log("마이크 시작: " + Microphone.devices[0]);
    }

    public void StopListening()
    {
        if (!isListening) return;
        isListening = false;
        Microphone.End(null);
        Debug.Log("마이크 중지");
    }

    IEnumerator WaitForMicAndRecognize()
    {
        // 마이크가 실제로 시작될 때까지 대기
        yield return new WaitUntil(() => Microphone.GetPosition(null) > 0);
        Debug.Log("마이크 입력 감지됨, 인식 시작");
        StartCoroutine(RecognizeLoop());
    }

    IEnumerator RecognizeLoop()
{
    int lastSample = 0;
    int clipSamples = micClip.samples;

    while (isListening)
    {
        yield return new WaitForSeconds(0.02f);

        int currentSample = Microphone.GetPosition(null);
        if (currentSample == lastSample) continue;

        float[] samples;

        if (currentSample > lastSample)
        {
            int sampleCount = currentSample - lastSample;
            samples = new float[sampleCount];
            micClip.GetData(samples, lastSample);
        }
        else
        {
            // 버퍼 순환 처리 - 안전하게 두 번에 나눠서 읽기
            int part1Count = clipSamples - lastSample;
            int part2Count = currentSample;

            // part1 읽기
            float[] part1 = new float[part1Count];
            if (part1Count > 0)
                micClip.GetData(part1, lastSample);

            // part2 읽기
            float[] part2 = new float[part2Count];
            if (part2Count > 0)
                micClip.GetData(part2, 0);

            // 합치기
            samples = new float[part1Count + part2Count];
            part1.CopyTo(samples, 0);
            part2.CopyTo(samples, part1Count);
        }

        // 빈 샘플 방지
        if (samples.Length == 0) continue;

        lastSample = currentSample;

        UpdateVAD(samples);

        byte[] bytes = ConvertToBytes(samples);
        if (recognizer.AcceptWaveform(bytes, bytes.Length))
        {
            ProcessResult(recognizer.Result());
        }
    }

    ProcessResult(recognizer.FinalResult());
}

    byte[] ConvertToBytes(float[] floatSamples)
    {
        short[] shorts = new short[floatSamples.Length];
        for (int i = 0; i < floatSamples.Length; i++)
            shorts[i] = (short)(Mathf.Clamp(floatSamples[i], -1f, 1f) * 32767f);

        byte[] bytes = new byte[shorts.Length * 2];
        System.Buffer.BlockCopy(shorts, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    void ProcessResult(string json)
    {
        if (string.IsNullOrEmpty(json)) return;
        if (!json.Contains("\"text\"")) return;

        int start = json.IndexOf(": \"") + 3;
        int end = json.LastIndexOf("\"");
        if (start >= end) return;

        string text = json.Substring(start, end - start).Trim();
        if (string.IsNullOrWhiteSpace(text)) return;

        // Debug.Log("인식 결과: " + text);
        OnSpeechRecognized(text);
    }

    void UpdateVAD(float[] samples)
    {
        // RMS(음량) 계산
        float rms = 0f;
        for (int i = 0; i < samples.Length; i++)
            rms += samples[i] * samples[i];
        rms = Mathf.Sqrt(rms / samples.Length);

        if (rms >= vadThreshold)
        {
            // 말하는 중
            silenceTimer = 0f;
            if (!IsSpeaking)
            {
                IsSpeaking = true;
                OnSpeakingStarted();
            }
        }
        else
        {
            // 조용한 상태
            if (IsSpeaking)
            {
                silenceTimer += 0.1f;
                if (silenceTimer >= silenceDuration)
                {
                    silenceTimer = 0f;
                    IsSpeaking = false;
                    OnSpeakingEnded();
                }
            }
        }
    }

    // 인식된 텍스트를 여기서 활용하세요
    void OnSpeechRecognized(string text)
    {
        // 예시: UI 텍스트 업데이트
        // resultText.text = text;

        // 예시: 커맨드 처리
        // if (text.Contains("fire")) Shoot();
        // if (text.Contains("jump")) Jump();
    }

    void OnSpeakingStarted()
    {
        Debug.Log("🔇 말하기 시작");

        fire = true;
    }

    // 말 끝날 때 호출
    void OnSpeakingEnded()
    {
        Debug.Log("🔇 말하기 종료");
        // 여기서 UI 변경, 애니메이션 등 처리
        fire = false;
    }

    void OnDestroy()
    {
        StopListening();
        recognizer?.Dispose();
        model?.Dispose();
    }
}