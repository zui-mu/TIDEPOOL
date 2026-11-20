using UnityEngine;
using UnityEngine.InputSystem;

public sealed class PoolLetterPickup : MonoBehaviour
{
    public PoolWaterCycle cycle;
    public GameObject envelopeVisual;
    public Light revealLight;
    public float pickupDistance = 2.1f;
    [TextArea(6,14)] public string letterText = "你还记得吗？\n\n你们说，只是没有人愿意和我一组。\n你们说，站在那里又不会怎么样。\n\n我所经历过的，这一次轮到你了。\n\n好好享受。";
    [SerializeField] private bool shortItalicNote;
    public bool PickedUp { get; private set; }
    public bool IsReading { get; private set; }
    public bool IsRevealed => cycle && cycle.CurrentPhase == PoolWaterCycle.Phase.Empty && !PickedUp;
    public bool IsInRange => cycle && cycle.player && Vector3.Distance(cycle.player.position, transform.position + Vector3.up * .8f) < pickupDistance;
    PlayerMovement movement;
    CameraFollow follow;
    bool movementWasEnabled, followWasEnabled;
    Font letterFont;
    GUIStyle body, title, hint;
    void Update()
    {
        if(envelopeVisual) envelopeVisual.SetActive(IsRevealed);
        if(revealLight) revealLight.enabled=IsRevealed;
        var keyboard=Keyboard.current;
        if(keyboard==null) return;
        if(IsReading)
        {
            if(keyboard.eKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame) CloseLetter();
            return;
        }
        if(keyboard.eKey.wasPressedThisFrame) TryPickUp();
    }
    public bool TryPickUp()
    {
        if(!IsRevealed || !IsInRange || !cycle.BeginFloodFromLetter()) return false;
        PickedUp=true; IsReading=true;
        if(envelopeVisual) envelopeVisual.SetActive(false);
        if(revealLight) revealLight.enabled=false;
        movement=cycle.player.GetComponent<PlayerMovement>(); follow=Camera.main ? Camera.main.GetComponent<CameraFollow>() : null;
        movementWasEnabled=movement && movement.enabled; followWasEnabled=follow && follow.enabled;
        if(movement) movement.enabled=false;
        if(follow) follow.enabled=false;
        return true;
    }
    public void CloseLetter()
    {
        if(!IsReading) return;
        IsReading=false;
        if(movement) movement.enabled=movementWasEnabled;
        if(follow) follow.enabled=followWasEnabled;
    }
    public void ResetLetter()
    {
        CloseLetter(); PickedUp=false;
        if(envelopeVisual) envelopeVisual.SetActive(false);
        if(revealLight) revealLight.enabled=false;
    }
    void OnDisable() { CloseLetter(); }
    void EnsureStyles()
    {
        if(body!=null)return;
        letterFont=Font.CreateDynamicFontFromOSFont(new[] {"Microsoft YaHei", "SimHei", "Arial"},24);
        body=new GUIStyle(GUI.skin.label) {font=letterFont,fontSize=23,wordWrap=true,alignment=TextAnchor.UpperLeft};
        body.normal.textColor=new Color(.13f,.17f,.2f);
        if (shortItalicNote)
        {
            body.font = Font.CreateDynamicFontFromOSFont(new[] { "Georgia", "Times New Roman", "Arial" }, 32);
            body.fontStyle = FontStyle.Italic;
            body.fontSize = 32;
            body.alignment = TextAnchor.MiddleCenter;
        }
        title=new GUIStyle(body){fontSize=30,alignment=TextAnchor.MiddleLeft};
        hint=new GUIStyle(GUI.skin.label){font=letterFont,fontSize=20,alignment=TextAnchor.MiddleCenter,wordWrap=true};
    }
    void OnGUI()
    {
        if(!IsReading && !(IsRevealed && IsInRange))return;
        EnsureStyles();
        if(!IsReading)
        {
            float hintWidth = Mathf.Min(510f, Screen.width - 32f);
            Rect hintBox = new Rect((Screen.width - hintWidth) / 2f, Screen.height - 170, hintWidth, 58f);
            GUI.Box(hintBox, GUIContent.none);
            GUI.Label(new Rect(hintBox.x + 12f, hintBox.y, hintBox.width - 24f, hintBox.height),
                "[E] Pick up the letter at the bottom of the pool", hint);
            return;
        }
        // The letter leaves a margin of the live scene visible. Water never pauses behind it.
        float scale=Mathf.Min(1f,Screen.height/850f);
        Matrix4x4 before=GUI.matrix;GUI.matrix=Matrix4x4.Scale(Vector3.one*scale);
        float width=Screen.width/scale,height=Screen.height/scale;
        float paperHeight = shortItalicNote ? 265f : 650f;
        float paperWidth = shortItalicNote ? Mathf.Min(640f, width - 48f) : 570f;
        Rect paper = new Rect((width - paperWidth) / 2f, Mathf.Max(24, (height - paperHeight) / 2), paperWidth, paperHeight);
        Color old=GUI.color;GUI.color=new Color(.9f,.92f,.9f,.98f);GUI.DrawTexture(paper,Texture2D.whiteTexture);GUI.color=old;
        if (shortItalicNote)
        {
            GUI.Label(new Rect(paper.x + 32f, paper.y + 24f, paper.width - 64f, 132f), letterText, body);
        }
        else
        {
            GUI.Label(new Rect(paper.x+48,paper.y+32,470,60),"致你",title);
            GUI.Label(new Rect(paper.x+48,paper.y+116,474,420),letterText,body);
        }
        string closeLabel = shortItalicNote ? "Close  [E / Esc]" : "放下信件，返回场景  [E / Esc]";
        float buttonWidth = Mathf.Min(330f, paper.width - 48f);
        if (GUI.Button(new Rect(paper.center.x - buttonWidth / 2f, paper.yMax - 80, buttonWidth, 44), closeLabel)) CloseLetter();
        GUI.matrix=before;
    }
}
