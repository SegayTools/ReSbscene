using UnityEngine;

public class NavigationCharacter : MonoBehaviour
{
    [HideInInspector]
    public int HashDefault = Animator.StringToHash("Navi_Default");

    [HideInInspector]
    public int HashWelcom = Animator.StringToHash("Navi_Welcom");

    [HideInInspector]
    public int HashFunStart = Animator.StringToHash("Navi_Fun_Start");

    [HideInInspector]
    public int HashSad01 = Animator.StringToHash("Navi_Sad_01");

    [HideInInspector]
    public int HashFunLoop = Animator.StringToHash("Navi_Fun_Loop_02");

    [SerializeField]
    private Animator[] _characterNaviAnimator;

    [SerializeField]
    private int _animationLayerIndex;

    [SerializeField]
    private Transform _emotionObject;

    [SerializeField]
    [Header("各アニメーション")]
    private AnimationClip _default;

    [SerializeField]
    private AnimationClip _funStart;

    [SerializeField]
    private AnimationClip _funLoop;

    [SerializeField]
    private AnimationClip _funEnd;

    [SerializeField]
    private AnimationClip _sad;

    public Transform EmotionObject => _emotionObject;

    public Animator[] NaviAnimator => _characterNaviAnimator;

    public AnimationClip Default => _default;

    public AnimationClip FunStart => _funStart;

    public AnimationClip FunLoop => _funLoop;

    public AnimationClip FunEnd => _funEnd;

    public AnimationClip Sad => _sad;
}