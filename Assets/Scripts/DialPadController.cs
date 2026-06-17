using System;
using UnityEngine;
using UnityEngine.UI;

public class DialPadController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Text inputText;
    [SerializeField] private int maxDigits = 3;
    [SerializeField] private string inputTextName = "DialInput";
    [SerializeField] private bool autoWireButtons = true;
    [SerializeField] private string callButtonName = "CallButton";
    [SerializeField] private string enterButtonName = "Enter";
    [SerializeField] private Color errorColor = Color.red;

    [Header("Audio")]
    [Tooltip("AudioSource yang akan memutar suara tombol. Jika kosong, akan dicari atau dibuat otomatis.")]
    [SerializeField] private AudioSource audioSource;
    [Tooltip("Suara feedback saat tombol angka ditekan (beep/klik).")]
    [SerializeField] private AudioClip dialBeepClip;

    public event Action<string> CallPressed;

    private string currentInput = string.Empty;
    private Color defaultInputColor = Color.white;

    private void Awake()
    {
        ResolveReferences();
        ResolveAudioSource();
        if (autoWireButtons)
        {
            AutoWireButtons();
        }

        if (inputText != null)
        {
            defaultInputColor = inputText.color;
        }

        UpdateDisplay();
    }

    private void ResolveAudioSource()
    {
        if (audioSource != null)
        {
            return;
        }

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }
    }

    private void ResolveReferences()
    {
        if (inputText != null || string.IsNullOrEmpty(inputTextName))
        {
            return;
        }

        Transform inputTransform = FindChildByName(transform, inputTextName);
        if (inputTransform != null)
        {
            inputText = inputTransform.GetComponent<Text>();
        }
    }

    private void AutoWireButtons()
    {
        for (int digit = 0; digit <= 9; digit++)
        {
            string digitName = digit.ToString();
            Transform digitTransform = FindChildByName(transform, digitName);
            if (digitTransform == null)
            {
                continue;
            }

            Button button = digitTransform.GetComponent<Button>();
            if (button == null)
            {
                continue;
            }

            string capturedDigit = digitName;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => AddDigit(capturedDigit));
        }

        Button callButton = FindButtonByName(callButtonName);
        if (callButton == null)
        {
            callButton = FindButtonByName(enterButtonName);
        }

        if (callButton != null)
        {
            callButton.onClick.RemoveAllListeners();
            callButton.onClick.AddListener(Call);
        }
    }

    private Button FindButtonByName(string buttonName)
    {
        if (string.IsNullOrEmpty(buttonName))
        {
            return null;
        }

        Transform buttonTransform = FindChildByName(transform, buttonName);
        if (buttonTransform == null)
        {
            return null;
        }

        return buttonTransform.GetComponent<Button>();
    }

    private Transform FindChildByName(Transform root, string childName)
    {
        if (root == null || string.IsNullOrEmpty(childName))
        {
            return null;
        }

        if (root.name == childName)
        {
            return root;
        }

        foreach (Transform child in root)
        {
            Transform found = FindChildByName(child, childName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    public void AddDigit(string digit)
    {
        if (string.IsNullOrEmpty(digit))
        {
            return;
        }

        if (currentInput.Length >= maxDigits)
        {
            return;
        }

        currentInput += digit;
        PlayDialBeep();
        UpdateDisplay();
    }

    private void PlayDialBeep()
    {
        if (audioSource == null || dialBeepClip == null)
        {
            return;
        }

        audioSource.PlayOneShot(dialBeepClip);
    }

    public void ClearInput()
    {
        currentInput = string.Empty;
        UpdateDisplay();
    }

    public void Backspace()
    {
        if (currentInput.Length == 0)
        {
            return;
        }

        currentInput = currentInput.Substring(0, currentInput.Length - 1);
        UpdateDisplay();
    }

    public void Call()
    {
        CallPressed?.Invoke(currentInput);
    }

    public void ResetInput()
    {
        currentInput = string.Empty;
        UpdateDisplay();
    }

    public void SetInputColor(Color color)
    {
        if (inputText == null)
        {
            return;
        }

        inputText.color = color;
    }

    public void ResetInputColor()
    {
        SetInputColor(defaultInputColor);
    }

    public void ShowErrorColor()
    {
        SetInputColor(errorColor);
    }

    public void SetVisible(bool isVisible)
    {
        gameObject.SetActive(isVisible);
    }

    private void UpdateDisplay()
    {
        if (inputText != null)
        {
            inputText.text = currentInput;
        }
    }
}
