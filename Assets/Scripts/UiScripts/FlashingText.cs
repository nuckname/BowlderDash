using UnityEngine;
using TMPro;

[RequireComponent(typeof(TextMeshProUGUI))]
public class FlashingText : MonoBehaviour
{
    private TextMeshProUGUI _textMesh;

    [Header("Flash Settings")]
    public float flashSpeed = 2f;
    public Color minColor = new Color(1f, 1f, 1f, 0f); 
    public Color maxColor = new Color(1f, 1f, 1f, 1f); 

    void Start()
    {
        if (_textMesh == null)
        {
            _textMesh = GetComponent<TextMeshProUGUI>();
        }
    }

    void Update()
    {
        float t = Mathf.PingPong(Time.time * flashSpeed, 1f);
        
        _textMesh.color = Color.Lerp(minColor, maxColor, t);
    }
}