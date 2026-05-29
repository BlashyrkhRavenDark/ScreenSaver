using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using AlbumCoverFinder;

namespace ScreenSaver
{
    /// <summary>
    /// Custom-painted PictureBox replacement that animates between the previous
    /// and current image using one of several transition effects. The effect and
    /// duration are per-instance so the focal tile and mosaic tiles can use
    /// different speeds.
    ///
    /// Effects:
    ///   Blink       - no animation, instant swap
    ///   FadeToBlack - old fades to black, then new fades in from black
    ///   Merge       - per-pixel color blend (alpha lerp) old -> new
    ///   Flip        - horizontal compress to 0 then back, swapping image at midpoint
    /// </summary>
    public class FadingPictureBox : Control
    {
        private Image m_oPrev;
        private Image m_oCurrent;
        private int m_iFadeStartTick;
        private bool m_bFading;
        private readonly Timer m_oFadeTimer;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int TransitionDurationMs { get; set; } = 1500;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public TransitionEffect Effect { get; set; } = TransitionEffect.Merge;

        private bool m_bHighlighted;
        private Color m_oHighlightColor = Color.FromArgb(255, 230, 90);

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool Highlighted
        {
            get { return m_bHighlighted; }
            set
            {
                if (m_bHighlighted == value) return;
                m_bHighlighted = value;
                if (m_bHighlighted) m_oFadeTimer.Start();
                Invalidate();
            }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color HighlightColor
        {
            get { return m_oHighlightColor; }
            set { m_oHighlightColor = value; if (m_bHighlighted) Invalidate(); }
        }

        public FadingPictureBox()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            BackColor = Color.Black;
            m_oFadeTimer = new Timer { Interval = 16 };
            m_oFadeTimer.Tick += OnFadeTick;
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Image Image
        {
            get { return m_oCurrent; }
            set
            {
                if (ReferenceEquals(m_oCurrent, value)) return;
                m_oPrev = m_oCurrent;
                m_oCurrent = value;

                if (Effect == TransitionEffect.Blink || m_oPrev == null || !IsHandleCreated)
                {
                    // Blink mode or initial set: no transition, just swap.
                    m_bFading = false;
                    Invalidate();
                    return;
                }

                m_iFadeStartTick = Environment.TickCount;
                m_bFading = true;
                m_oFadeTimer.Start();
                Invalidate();
            }
        }

        /// <summary>
        /// Sets the image instantly with no transition - used to seed initial tiles.
        /// </summary>
        public void SetImageImmediate(Image img)
        {
            m_oPrev = null;
            m_oCurrent = img;
            m_bFading = false;
            Invalidate();
        }

        private void OnFadeTick(object sender, EventArgs e)
        {
            int elapsed = Environment.TickCount - m_iFadeStartTick;
            if (elapsed >= TransitionDurationMs)
            {
                m_bFading = false;
                m_oPrev = null;
                if (!m_bHighlighted) m_oFadeTimer.Stop();
            }
            Invalidate();
        }

        private static int s_iPaintLogCount;
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            var g = e.Graphics;
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;

            Rectangle dest = ClientRectangle;

            if (s_iPaintLogCount < 5)
            {
                s_iPaintLogCount++;
                DiagLog.Write("FadingPictureBox.OnPaint #" + s_iPaintLogCount + " Effect=" + Effect + " currentNull=" + (m_oCurrent == null) + " prevNull=" + (m_oPrev == null) + " fading=" + m_bFading + " dest=" + dest);
            }

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
                DrawHighlightBorder(g);
        }

        private void PaintMerge(Graphics g, Rectangle dest, float t)
        {
            // Per-pixel alpha lerp: equivalent to "compute color difference and gradually
            // adjust" - that's exactly what alpha blending two RGB layers does.
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
            // Black background fills the dest while either image is partially transparent.
            using (var brush = new SolidBrush(Color.Black)) g.FillRectangle(brush, dest);
            if (t < 0.5f)
            {
                // Old fades out: alpha 1 -> 0 over the first half.
                if (m_oPrev != null)
                {
                    float alpha = 1f - (t * 2f);
                    DrawImageWithAlpha(g, m_oPrev, dest, alpha);
                }
            }
            else
            {
                // New fades in: alpha 0 -> 1 over the second half.
                if (m_oCurrent != null)
                {
                    float alpha = (t - 0.5f) * 2f;
                    DrawImageWithAlpha(g, m_oCurrent, dest, alpha);
                }
            }
        }

        private void PaintFlip(Graphics g, Rectangle dest, float t)
        {
            if (!m_bFading)
            {
                if (m_oCurrent != null) g.DrawImage(m_oCurrent, dest);
                return;
            }
            // Horizontal accordion: scaleX = |2t - 1|. 1 at t=0, 0 at t=0.5, 1 at t=1.
            // First half shows the old image shrinking; second half shows the new image expanding.
            // The black background hides the tile while the image is edge-on.
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

        private void DrawHighlightBorder(Graphics g)
        {
            float phase = (Environment.TickCount % 1400) / 1400f;
            float alphaF = 0.55f + 0.45f * (float)Math.Sin(phase * Math.PI * 2);
            int alpha = Math.Max(0, Math.Min(255, (int)(alphaF * 255)));
            Color borderColor = Color.FromArgb(alpha, m_oHighlightColor);

            int thickness = Math.Max(3, ClientSize.Width / 32);
            using (var pen = new System.Drawing.Pen(borderColor, thickness))
            {
                pen.Alignment = System.Drawing.Drawing2D.PenAlignment.Inset;
                var r = new Rectangle(0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
                g.DrawRectangle(pen, r);
            }
            if (!m_bFading && m_bHighlighted && !m_oFadeTimer.Enabled)
                m_oFadeTimer.Start();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                m_oFadeTimer.Stop();
                m_oFadeTimer.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
