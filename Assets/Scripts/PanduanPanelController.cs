using UnityEngine;
using UnityEngine.UI;

namespace Flare.UI
{
    public class PanduanPanelController : MonoBehaviour
    {
        [Header("Panel Root")]
        [Tooltip("Root GameObject panel panduan (ini sendiri atau parent-nya)")]
        public GameObject panelRoot;

        [Header("Slide Content")]
        [Tooltip("Image yang menampilkan gambar tutorial aktif")]
        public Image slideImage;

        [Tooltip("7 gambar tutorial (tutorial_1 s/d tutorial_7). Isi sesuai urutan.")]
        public Sprite[] tutorialSprites = new Sprite[7];

        [Header("Caption Per Slide")]
        [Tooltip("Text (Legacy) untuk menampilkan keterangan slide. Kosongkan jika tidak pakai.")]
        public Text captionText;

        [Tooltip("Isi keterangan teks untuk setiap slide (opsional, boleh dikosongkan).")]
        [TextArea(2, 4)]
        public string[] slideCaptions = new string[7];

        [Header("Navigasi")]
        [Tooltip("Tombol panah ke slide sebelumnya")]
        public Button prevButton;

        [Tooltip("Tombol panah ke slide berikutnya")]
        public Button nextButton;

        [Header("Dot Indicator")]
        [Tooltip("Parent container dot indicator (gunakan Horizontal Layout Group)")]
        public Transform dotContainer;

        [Tooltip("Prefab dot (Image sederhana berbentuk lingkaran kecil)")]
        public GameObject dotPrefab;

        [Tooltip("Warna dot pada slide yang sedang aktif")]
        public Color dotActiveColor = new Color(1f, 0.6f, 0.1f, 1f);   // oranye

        [Tooltip("Warna dot pada slide yang tidak aktif")]
        public Color dotInactiveColor = new Color(1f, 1f, 1f, 0.35f);  // putih transparan

        [Header("Tombol Tutup")]
        [Tooltip("Tombol untuk menutup panel panduan")]
        public Button closeButton;

        // ── state internal ──────────────────────────────────────────────────
        private int _currentIndex = 0;
        private Image[] _dots;

        // ── lifecycle ───────────────────────────────────────────────────────

        private void Awake()
        {
            // Jika panelRoot tidak diset, gunakan GameObject ini sendiri
            if (panelRoot == null)
                panelRoot = gameObject;

            // Wiring tombol via script (backup jika belum di-wire di Inspector)
            if (prevButton != null) prevButton.onClick.AddListener(PrevSlide);
            if (nextButton != null) nextButton.onClick.AddListener(NextSlide);
            if (closeButton != null) closeButton.onClick.AddListener(HidePanel);

            BuildDots();
        }

        private void OnEnable()
        {
            // Reset ke slide pertama setiap kali panel dibuka
            GoToSlide(0);
        }

        // ── public API ──────────────────────────────────────────────────────

        /// <summary>Tampilkan panel panduan.</summary>
        public void ShowPanel()
        {
            panelRoot.SetActive(true);
        }

        /// <summary>Sembunyikan panel panduan.</summary>
        public void HidePanel()
        {
            panelRoot.SetActive(false);
        }

        /// <summary>Pindah ke slide berikutnya.</summary>
        public void NextSlide()
        {
            if (_currentIndex < tutorialSprites.Length - 1)
                GoToSlide(_currentIndex + 1);
        }

        /// <summary>Pindah ke slide sebelumnya.</summary>
        public void PrevSlide()
        {
            if (_currentIndex > 0)
                GoToSlide(_currentIndex - 1);
        }

        // ── internal ────────────────────────────────────────────────────────

        private void GoToSlide(int index)
        {
            if (tutorialSprites == null || tutorialSprites.Length == 0)
            {
                Debug.LogWarning("[PanduanPanel] tutorialSprites kosong – assign di Inspector.", this);
                return;
            }

            // Clamp index
            _currentIndex = Mathf.Clamp(index, 0, tutorialSprites.Length - 1);

            // Update gambar
            if (slideImage != null && tutorialSprites[_currentIndex] != null)
                slideImage.sprite = tutorialSprites[_currentIndex];

            // Update caption
            if (captionText != null)
            {
                bool hasCaption = slideCaptions != null
                                  && _currentIndex < slideCaptions.Length
                                  && !string.IsNullOrEmpty(slideCaptions[_currentIndex]);

                captionText.text = hasCaption ? slideCaptions[_currentIndex] : string.Empty;
                captionText.gameObject.SetActive(hasCaption);
            }

            // Update navigasi
            if (prevButton != null)
                prevButton.interactable = _currentIndex > 0;

            if (nextButton != null)
                nextButton.interactable = _currentIndex < tutorialSprites.Length - 1;

            // Update dots
            RefreshDots();
        }

        private void BuildDots()
        {
            if (dotContainer == null || dotPrefab == null) return;

            // Bersihkan dot lama (jika ada dari editor)
            foreach (Transform child in dotContainer)
                Destroy(child.gameObject);

            int count = tutorialSprites != null ? tutorialSprites.Length : 0;
            _dots = new Image[count];

            for (int i = 0; i < count; i++)
            {
                GameObject dot = Instantiate(dotPrefab, dotContainer);
                dot.name = $"Dot_{i}";
                _dots[i] = dot.GetComponent<Image>();
            }
        }

        private void RefreshDots()
        {
            if (_dots == null) return;

            for (int i = 0; i < _dots.Length; i++)
            {
                if (_dots[i] != null)
                    _dots[i].color = (i == _currentIndex) ? dotActiveColor : dotInactiveColor;
            }
        }
    }
}
