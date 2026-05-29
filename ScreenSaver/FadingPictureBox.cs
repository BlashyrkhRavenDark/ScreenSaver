using System;
using System.Drawing;
using System.Drawing.Imaging;
using AlbumCoverFinder;

namespace ScreenSaver
{
    /// <summary>
    /// A non-control tile renderer that animates between a previous and current
    /// image using one of several transition effects, plus an optional pulsing
    /// highlight border.
    ///
    /// IMPORTANT - why this is NOT a Control: on this machine's .NET 10 runtime,
    /// child controls of the screensaver form never composite to the screen (the
    /// form paints, the children do not - verified on-screen with every control
    /// type, DPI mode, and window style). So the mosaic is owner-drawn: the form
    /// holds an array of these tiles and calls <see cref="Paint"/> for each one in
    /// its own OnPaint. The form composites reliably; children never did.
    ///
    /// Effects:
    ///   Blink       - no animation, instant swap
    ///   FadeToBlack - old fades to black, then new fades in from black
    ///   Merge       - per-pixel color blend (alpha lerp) old -> new
    ///   Flip        - horizontal compress to 0 then back, swapping image at midpoint
    /// </summary>
    public class FadingTile
    {
        private Image m_oPrev;
        private Image m_oCurrent;
        private int m_iFadeStartTick;
        private bool m_bFading;

        public int TransitionDurationMs { get; set; } = 1500;
        public TransitionEffect Effect { get; set; } = TransitionEffect.Merge;

        private bool m_bHighlighted;
        public Color HighlightColor { get; set; } = Color.FromArgb(255, 230, 90);

        public bool Highlighted
        {
            get { return m_bHighlighted; }
            set { m_bHighlighted = value; }
        }

        /// <summary>The current (settled) image. Null until first set.</summary>
        public Image Current { get { return m_oCurrent; } }

        /// <summary>True while a transition is in progress or a highlight is pulsing -
        /// i.e. the form needs to keep repainting this tile.</summary>
        public bool Animating { get { return m_bFading || m_bHighlighted; } }

        /// <summary>Start a transition to a new image (or instant swap for Blink/first set).</summary>
        public void SetImage(Image img)
        {
            if (ReferenceEquals(m_oCurrent, img)) return;
            m_oPrev = m_oCurrent;
            m_oCurrent = img;

            if (Effect == TransitionEffect.Blink || m_oPrev == null)
            {
                m_bFading = false;
                return;
            }
            m_iFadeStartTick = Environment.TickCount;
            m_bFading = true;
        }

        /// <summary>Set the image instantly with no transition - used to seed tiles.</summary>
        public void SetImageImmediate(Image img)
        {
            m_oPrev = null;
            m_oCurrent = img;
            m_bFading = false;
        }

        /// <summary>Advance the transition clock; clears the fade once complete.
        /// Called once per animation frame by the form.</summary>
        public void Advance()
        {
            if (m_bFading && Environment.TickCount - m_iFadeStartTick >= TransitionDurationMs)
            {
                m_bFading = false;
                m_oPrev = null;
            }
        }

        /// <summary>Draw the current frame into <paramref name="dest"/> on the supplied
        /// Graphics. The caller is responsible for setting interpolation/smoothing.</summary>
        public void Paint(Graphics g, Rectangle dest)
        {
            float t = 1f;
            if (m_bFading && TransitionDurationMs > 0)
            {
                int elapsed = Environment.TickCount - m_iFadeStartTick;
                t = Math.Min(1f, elapsed / (float)TransitionDurationMs);
            }

            switch (Effect)
            {
                case TransitionEffect.Blink:
                    if (m_oCurrent != null) g.DrawImage(m_oCurrent, dest);
                    break;
                case TransitionEffect.Merge:
                    PaintMerge(g, dest, t);
                    break;
                case TransitionEffect.FadeToBlack:
                    PaintFadeToBlack(g, dest, t);
                    break;
                case TransitionEffect.Flip:
                    PaintFlip(g, dest, t);
                    break;
            }

            if (m_bHighlighted)
                DrawHighlightBorder(g, dest);
        }

        private void PaintMerge(Graphics g, Rectangle dest, float t)
        {
            // Ease out for a gentler arrival.
            float teased = 1f - (1f - t) * (1f - t) * (1f - t);
            if (m_bFading && m_oPrev != null)
                DrawImageWithAlpha(g, m_oPrev, dest, 1f - teased);
            if (m_oCurrent != null)
            {
                if (m_bFading) DrawImageWithAlpha(g, m_oCurrent, dest, teased);
                else g.DrawImage(m_oCurrent, dest);
            }
        }

        private void PaintFadeToBlack(Graphics g, Rectangle dest, float t)
        {
            if (!m_bFading)
            {
                if (m_oCurrent != null) g.DrawImage(m_oCurrent, dest);
                return;
            }
            using (var brush = new SolidBrush(Color.Black)) g.FillRectangle(brush, dest);
            if (t < 0.5f)
            {
                if (m_oPrev != null)
                    DrawImageWithAlpha(g, m_oPrev, dest, 1f - (t * 2f));
            }
            else
            {
                if (m_oCurrent != null)
                    DrawImageWithAlpha(g, m_oCurrent, dest, (t - 0.5f) * 2f);
            }
        }

        private void PaintFlip(Graphics g, Rectangle dest, float t)
        {
            if (!m_bFading)
            {
                if (m_oCurrent != null) g.DrawImage(m_oCurrent, dest);
                return;
            }
            using (var brush = new SolidBrush(Color.Black)) g.FillRectangle(brush, dest);

            float scaleX = Math.Abs(2f * t - 1f);
            int w = Math.Max(1, (int)(dest.Width * scaleX));
            int x = dest.X + (dest.Width - w) / 2;
            var stretched = new Rectangle(x, dest.Y, w, dest.Height);

            Image img = t < 0.5f ? m_oPrev : m_oCurrent;
            if (img != null) g.DrawImage(img, stretched);
        }

        private static void DrawImageWithAlpha(Graphics g, Image img, Rectangle dest, float alpha)
        {
            if (alpha <= 0f || img == null) return;
            if (alpha >= 1f)
            {
                g.DrawImage(img, dest);
                return;
            }
            var cm = new ColorMatrix { Matrix33 = alpha };
            using (var ia = new ImageAttributes())
            {
                ia.SetColorMatrix(cm);
                g.DrawImage(img, dest, 0, 0, img.Width, img.Height, GraphicsUnit.Pixel, ia);
            }
        }

        private void DrawHighlightBorder(Graphics g, Rectangle dest)
        {
            float phase = (Environment.TickCount % 1400) / 1400f;
            float alphaF = 0.55f + 0.45f * (float)Math.Sin(phase * Math.PI * 2);
            int alpha = Math.Max(0, Math.Min(255, (int)(alphaF * 255)));
            Color borderColor = Color.FromArgb(alpha, HighlightColor);

            int thickness = Math.Max(3, dest.Width / 32);
            using (var pen = new Pen(borderColor, thickness))
            {
                pen.Alignment = System.Drawing.Drawing2D.PenAlignment.Inset;
                var r = new Rectangle(dest.X, dest.Y, dest.Width - 1, dest.Height - 1);
                g.DrawRectangle(pen, r);
            }
        }
    }
}
