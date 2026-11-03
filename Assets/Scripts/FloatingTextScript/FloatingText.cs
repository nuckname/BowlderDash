using System;
using TMPro;
using UnityEngine;
using Random = UnityEngine.Random;

public class FloatingText : MonoBehaviour
{
    public enum MoveStyle { StraightUp, ZigZag, PopAndDrop }
    
    [Header("General Settings")]
    public MoveStyle style = MoveStyle.StraightUp;
    public float lifetime = 1.5f;

    [Header("Straight Up Settings")]
    public float floatSpeed = 2f;

    [Header("ZigZag Settings")]
    public float upwardSpeed = 1.5f;
    public float swayFrequency = 5f; // How fast it wiggles side to side
    public float swayAmplitude = 0.5f; // How wide the wiggle is

    [Header("Pop & Drop Settings")]
    public Vector2 popForce = new Vector2(1.5f, 4f);
    public float gravity = 8f;

    private float timer;
    private Vector3 startPos;
    private Vector3 currentVelocity;

    public TextMeshPro textComponent;

    private void Awake()
    {
        textComponent = GetComponent<TextMeshPro>();
        if (textComponent == null) return;

        // Draw on top of world geometry
        var mat = textComponent.fontMaterial; // instance, so it doesn't change the shared font material
        mat.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
        textComponent.renderer.sortingOrder = 100;
    }

    // Report initialization failure so scoring cannot sound for a label that was never created.
    public bool TryInitialize(string newText)
    {
        if (textComponent == null) return false;
        Initialize(newText);
        return true;
    }

    public void Initialize(string newText)
    {
        textComponent.text = newText;
        /*
        switch (type)
        {
            case ScoreCalculator.ScoreType.Normal:
                textComponent.color = Color.white;
                break;
            case ScoreCalculator.ScoreType.Critical:
                textComponent.color = Color.yellow;
                textComponent.fontSize *= 1.5f;
                style = MoveStyle.PopAndDrop;
                break;
        }*/
        
    }

    void Start()
    {
        startPos = transform.position;

        // Initialize physics for the Pop & Drop style
        if (style == MoveStyle.PopAndDrop)
        {
            // Give it a random left/right arc so they don't all stack on top of each other
            float randomX = Random.Range(-popForce.x, popForce.x);
            currentVelocity = new Vector3(randomX, popForce.y, 0);
        }

        // Clean up the object after its lifetime expires
        Destroy(gameObject, lifetime);
    }

    void Update()
    {
        if (GameplayPause.IsPaused(this)) return;
        timer += Time.deltaTime;

        switch (style)
        {
            case MoveStyle.StraightUp:
                transform.position += Vector3.up * floatSpeed * Time.deltaTime;
                break;

            case MoveStyle.ZigZag:
                // Move up linearly, but use a Sine wave for the X axis
                float yOffset = upwardSpeed * timer;
                float xOffset = Mathf.Sin(timer * swayFrequency) * swayAmplitude;
                transform.position = startPos + new Vector3(xOffset, yOffset, 0);
                break;

            case MoveStyle.PopAndDrop:
                // Apply faux gravity to the Y velocity, then move the object
                currentVelocity.y -= gravity * Time.deltaTime;
                transform.position += currentVelocity * Time.deltaTime;
                break;
        }
    }

    void LateUpdate()
    {
        if (GameplayPause.IsPaused(this)) return;
        // Makes sure the text always perfectly faces the camera. 
        // If it doesn't face the camera, the flat mesh can visually slice into walls and ruin depth perception.
        if (Camera.main != null)
        {
            transform.rotation = Camera.main.transform.rotation;
        }
    }
}
