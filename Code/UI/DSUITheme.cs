// ============================================================

using UnityEngine;

namespace Code.UI
{
    /// <summary>
    /// 登神长阶UI主题  柔和现代风格
    /// 低饱和深蓝灰背景 + 暖白文字 + 低饱和强调色
    /// </summary>
    public static class DSUITheme
    {
        // === 背景层（柔和深蓝灰，低饱和护眼）===
        public static readonly Color WindowBg = new Color(0.12f, 0.13f, 0.16f, 1f);      // 窗口底色  深蓝灰
        public static readonly Color PanelBg = new Color(0.15f, 0.16f, 0.20f, 1f);       // 面板底色  中蓝灰
        public static readonly Color CardBg = new Color(0.18f, 0.19f, 0.23f, 1f);        // 卡片底色  亮蓝灰
        public static readonly Color CardBgActive = new Color(0.31f, 0.29f, 0.23f, 1f);   // 选中卡片底色  柔和金褐
        public static readonly Color InputBg = new Color(0.10f, 0.11f, 0.14f, 1f);           // 输入框底色  深蓝灰
        public static readonly Color HoverBg = new Color(0.23f, 0.24f, 0.29f, 1f);        // 悬停底色  亮蓝灰

        // === 体系颜色（柔和）===
                        public static readonly Color EnergyColor = new Color(0.50f, 0.72f, 0.75f, 1f);       // 异能能量  灰青

        // === 文本层（暖白系，替代刺眼金色）===
        public static readonly Color TitleText = new Color(0.95f, 0.93f, 0.88f, 1f);         // 标题文字  米白
        public static readonly Color BodyText = new Color(0.85f, 0.85f, 0.82f, 1f);          // 正文文字  暖白
        public static readonly Color SecondaryText = new Color(0.66f, 0.67f, 0.66f, 1f);     // 次要文字  柔和灰
        public static readonly Color DimText = new Color(0.50f, 0.51f, 0.51f, 1f);           // 最淡文字  暗灰

        // === 强调色（低饱和点缀，少量金色）===
        public static readonly Color AccentBlue = new Color(0.62f, 0.72f, 0.85f, 1f);        // 主强调  柔蓝
        public static readonly Color AccentPurple = new Color(0.58f, 0.54f, 0.74f, 1f);      // 次强调  柔紫
        public static readonly Color AccentCyan = new Color(0.52f, 0.70f, 0.78f, 1f);        // 亮强调  灰青
        public static readonly Color AccentGold = new Color(0.88f, 0.78f, 0.55f, 1f);        // 金色强调  柔金
        public static readonly Color DangerRed = new Color(0.85f, 0.50f, 0.48f, 1f);         // 危险  柔和红
        public static readonly Color SuccessGreen = new Color(0.58f, 0.78f, 0.60f, 1f);      // 成功  柔和绿
        public static readonly Color WarningOrange = new Color(0.88f, 0.66f, 0.45f, 1f);     // 警告  柔和橙

        // === 边框/分割线（柔灰）===
        public static readonly Color BorderLight = new Color(0.40f, 0.42f, 0.48f, 1f);     // 亮色边框  柔灰
        public static readonly Color BorderDark = new Color(0.22f, 0.23f, 0.27f, 1f);        // 深色边框  深灰
        public static readonly Color Separator = new Color(0.32f, 0.34f, 0.38f, 0.9f);       // 分割线  中灰

        // === 境界颜色（13阶柔和渐变，低饱和护眼）===
        public static Color GetTierColor(int tier)
        {
            switch (tier)
            {
                case 1: return new Color(0.55f, 0.70f, 0.90f);      // 觉醒者  柔雾蓝
                case 2: return new Color(0.50f, 0.74f, 0.88f);      // 共振者  灰青
                case 3: return new Color(0.50f, 0.76f, 0.70f);      // 强化者  青绿
                case 4: return new Color(0.56f, 0.78f, 0.60f);      // 突变者  柔绿
                case 5: return new Color(0.70f, 0.78f, 0.52f);      // 调控者  黄绿
                case 6: return new Color(0.82f, 0.76f, 0.48f);      // 场域者  柔金
                case 7: return new Color(0.85f, 0.68f, 0.46f);      // 具象者  柔橙
                case 8: return new Color(0.84f, 0.58f, 0.42f);      // 干涉者  陶土
                case 9: return new Color(0.82f, 0.52f, 0.48f);      // 解析者  柔和红
                case 10: return new Color(0.80f, 0.50f, 0.60f);     // 使徒级  柔玫红
                case 11: return new Color(0.65f, 0.54f, 0.76f);     // 登神级  柔紫
                case 12: return new Color(0.55f, 0.52f, 0.82f);     // 真神级  柔蓝紫
                case 13: return new Color(0.88f, 0.80f, 0.55f);     // 真神  柔金
                default: return BodyText;
            }
        }

        // === 纹理生成（运行时创建渐变纹理，无需外部贴图）===
        private static Texture2D _windowBgTexture;
        private static Texture2D _cardBgTexture;
        private static Texture2D _titleBarBgTexture;
        private static Texture2D _buttonNormalTexture;
        private static Texture2D _buttonHoverTexture;
        private static Texture2D _buttonActiveTexture;
        private static Texture2D _inputBgTexture;

        public static Texture2D WindowBgTexture
        {
            get
            {
                if (_windowBgTexture == null)
                    _windowBgTexture = MakeSolidTexture(WindowBg); // 实心（与基因图谱一致）
                return _windowBgTexture;
            }
        }


        public static Texture2D TitleBarBgTexture
        {
            get
            {
                if (_titleBarBgTexture == null)
                    _titleBarBgTexture = MakeSolidTexture(new Color(0.16f, 0.17f, 0.21f, 1f)); // 标题栏（基因图谱同款）
                return _titleBarBgTexture;
            }
        }

        public static Texture2D CardBgTexture
        {
            get
            {
                if (_cardBgTexture == null)
                    _cardBgTexture = MakeSolidTexture(CardBg);
                return _cardBgTexture;
            }
        }

        public static Texture2D ButtonNormalTexture
        {
            get
            {
                if (_buttonNormalTexture == null)
                    _buttonNormalTexture = MakeGradientTexture(new Color(0.28f, 0.30f, 0.36f, 1f), new Color(0.20f, 0.21f, 0.26f, 1f));
                return _buttonNormalTexture;
            }
        }

        public static Texture2D ButtonHoverTexture
        {
            get
            {
                if (_buttonHoverTexture == null)
                    _buttonHoverTexture = MakeGradientTexture(new Color(0.34f, 0.36f, 0.42f, 1f), new Color(0.26f, 0.28f, 0.33f, 1f));
                return _buttonHoverTexture;
            }
        }

        public static Texture2D ButtonActiveTexture
        {
            get
            {
                if (_buttonActiveTexture == null)
                    _buttonActiveTexture = MakeGradientTexture(AccentGold, new Color(0.68f, 0.60f, 0.42f, 1f));
                return _buttonActiveTexture;
            }
        }

        public static Texture2D InputBgTexture
        {
            get
            {
                if (_inputBgTexture == null)
                    _inputBgTexture = MakeSolidTexture(InputBg);
                return _inputBgTexture;
            }
        }

        // 蓝色强调纹理（用于进度条等）
        private static Texture2D _accentBlueTexture;
        public static Texture2D AccentBlueTexture
        {
            get
            {
                if (_accentBlueTexture == null)
                    _accentBlueTexture = MakeSolidTexture(AccentBlue);
                return _accentBlueTexture;
            }
        }

        // 根据境界获取渐变纹理（用于境界卡片图标）
        private static readonly Texture2D[] _tierGradientTextures = new Texture2D[14];
        public static Texture2D GetTierGradientTexture(int tier)
        {
            if (tier < 1 || tier > 13) tier = 1;
            if (_tierGradientTextures[tier] == null)
            {
                Color tierColor = GetTierColor(tier);
                Color darkColor = new Color(tierColor.r * 0.35f, tierColor.g * 0.35f, tierColor.b * 0.35f, 1f);
                _tierGradientTextures[tier] = MakeGradientTexture(tierColor, darkColor);
            }
            return _tierGradientTextures[tier];
        }

        // === 纹理生成工具 ===
        private static Texture2D MakeSolidTexture(Color color)
        {
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var pixels = new Color[16];
            for (int i = 0; i < 16; i++) pixels[i] = color;
            tex.SetPixels(pixels);
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Repeat;
            return tex;
        }

        private static Texture2D MakeGradientTexture(Color top, Color bottom)
        {
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var pixels = new Color[16];
            for (int y = 0; y < 4; y++)
            {
                float t = y / 3f;
                Color c = Color.Lerp(top, bottom, t);
                for (int x = 0; x < 4; x++)
                    pixels[y * 4 + x] = c;
            }
            tex.SetPixels(pixels);
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Repeat;
            return tex;
        }
    }
}



