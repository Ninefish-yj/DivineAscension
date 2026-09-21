// ============================================================

using UnityEngine;
using Code.Core;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Code.UI
{
    /// <summary>
    /// 基因图谱按钮拖动处理
    /// 让基因图谱按钮可以被鼠标拖动
    /// </summary>
    public class GeneButtonDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private RectTransform _rectTransform;
        private Vector2 _dragOffset;
        private bool _isDragging = false;
        private float _dragStartTime = 0f;

        // PlayerPrefs键名
        private const string PREFS_X = "DS_GeneButton_X";
        private const string PREFS_Y = "DS_GeneButton_Y";
        private const string PREFS_INITIALIZED = "DS_GeneButton_Initialized";

        // 默认位置
        public static readonly Vector2 DefaultPosition = new Vector2(156f, -84f);

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
        }

        private void Start()
        {
            // 加载保存的位置
            LoadPosition();
        }

        /// <summary>开始拖动</summary>
        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_rectTransform == null) return;

            _isDragging = true;
            _dragStartTime = Time.time;

            // 计算鼠标和按钮中心的偏移
            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _rectTransform,
                eventData.position,
                eventData.pressEventCamera,
                out localPoint
            );
            _dragOffset = localPoint;
        }

        /// <summary>拖动中</summary>
        public void OnDrag(PointerEventData eventData)
        {
            if (_rectTransform == null || !_isDragging) return;

            // 将鼠标位置转换为父对象的本地坐标
            RectTransform parentRect = _rectTransform.parent as RectTransform;
            if (parentRect == null) return;

            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRect,
                eventData.position,
                eventData.pressEventCamera,
                out localPoint
            );

            // 更新位置（减去偏移，让按钮保持在鼠标点击的位置）
            _rectTransform.anchoredPosition = new Vector2(
                localPoint.x - _dragOffset.x,
                localPoint.y - _dragOffset.y
            );
        }

        /// <summary>拖动结束</summary>
        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_isDragging) return;

            _isDragging = false;

            // 保存位置
            SavePosition();

            // 如果拖动时间很短（<0.2秒），不认为是拖动，允许点击事件触发
            float dragDuration = Time.time - _dragStartTime;
            if (dragDuration < 0.2f)
            {
                // 短时间拖动，可能是点击，不阻止点击
            }
        }

        /// <summary>保存位置到PlayerPrefs</summary>
        private void SavePosition()
        {
            if (_rectTransform == null) return;

            try
            {
                PlayerPrefs.SetFloat(PREFS_X, _rectTransform.anchoredPosition.x);
                PlayerPrefs.SetFloat(PREFS_Y, _rectTransform.anchoredPosition.y);
                PlayerPrefs.SetInt(PREFS_INITIALIZED, 1);
                PlayerPrefs.Save();
            }
            catch (System.Exception e)
            {
                DSDebug.Warning("[DivineAscension] 保存基因按钮位置失败: " + e.Message);
            }
        }

        /// <summary>从PlayerPrefs加载位置</summary>
        private void LoadPosition()
        {
            if (_rectTransform == null) return;

            try
            {
                if (PlayerPrefs.GetInt(PREFS_INITIALIZED, 0) == 1)
                {
                    float x = PlayerPrefs.GetFloat(PREFS_X, DefaultPosition.x);
                    float y = PlayerPrefs.GetFloat(PREFS_Y, DefaultPosition.y);
                    _rectTransform.anchoredPosition = new Vector2(x, y);
                }
                else
                {
                    _rectTransform.anchoredPosition = DefaultPosition;
                }
            }
            catch (System.Exception e)
            {
                DSDebug.Warning("[DivineAscension] 加载基因按钮位置失败: " + e.Message);
                _rectTransform.anchoredPosition = DefaultPosition;
            }
        }

        /// <summary>重置位置到默认值（静态方法，供外部调用）</summary>

    }
}
