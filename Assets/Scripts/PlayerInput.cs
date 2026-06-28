using UnityEngine;

public class PlayerInput : MonoBehaviour {
    public string moveAxisName = "Vertical"; // 앞뒤 움직임을 위한 입력축 이름
    public string rotateAxisName = "Horizontal"; // 좌우 회전을 위한 입력축 이름
    public string fireButtonName = "Fire1"; // 발사를 위한 입력 버튼 이름
    public string reloadButtonName = "Reload"; // 재장전을 위한 입력 버튼 이름

    // 값 할당은 내부에서만 가능
    public float move { get; private set; } // 감지된 움직임 입력값
    public float rotate { get; private set; } // 감지된 회전 입력값
    public bool fire { get; private set; } // 감지된 발사 입력값
    public bool reload { get; private set; } // 감지된 재장전 입력값

    // ─── 음성 주입용 (VoiceCommandInjector가 여기에 씁니다) ───
    [HideInInspector] public float voiceMove = 0f;
    [HideInInspector] public float voiceRotate = 0f;
    [HideInInspector] public bool voiceFire = false;
    [HideInInspector] public bool voiceReload = false;
    // ──────────────────────────────────────────────────────────

    // 매프레임 사용자 입력을 감지
    private void Update() {
        // 게임오버 상태에서는 사용자 입력을 감지하지 않는다
        if (GameManager.instance != null
            && GameManager.instance.isGameover)
        {
            move = 0;
            rotate = 0;
            fire = false;
            reload = false;
            return;
        }

        // move에 관한 입력 감지 (키보드 + 음성 합산)
        move = Input.GetAxis(moveAxisName) + voiceMove;
        // rotate에 관한 입력 감지 (키보드 + 음성 합산)
        rotate = Input.GetAxis(rotateAxisName) + voiceRotate;
        // fire에 관한 입력 감지 (키보드 또는 음성)
        fire = voiceFire;
        // reload에 관한 입력 감지 (키보드 또는 음성)
        reload = voiceReload;

        // 음성 단발성 입력은 한 프레임 뒤 자동 해제
        voiceFire = false;
        voiceReload = false;
    }
}