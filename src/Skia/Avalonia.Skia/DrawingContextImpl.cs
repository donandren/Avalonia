using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.RenderHelpers;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Avalonia.Skia
{
    internal class DrawingContextImpl : IDrawingContextImpl
    {
        public SKCanvas Canvas { get; private set; }

        public DrawingContextImpl(SKCanvas canvas)
        {
            Canvas = canvas;
            Canvas.Clear();
        }

        public void DrawImage(IBitmap source, double opacity, Rect sourceRect, Rect destRect)
        {
            var impl = (BitmapImpl)source.PlatformImpl;
            var s = sourceRect.ToSKRect();
            var d = destRect.ToSKRect();
            using (var paint = new SKPaint()
                    { Color = new SKColor(255, 255, 255, (byte)(255 * opacity)) })
            {
                Canvas.DrawBitmap(impl.Bitmap, s, d, paint);
            }
        }

        public void DrawLine(Pen pen, Point p1, Point p2)
        {
            using (var paint = CreatePaint(pen, new Size(Math.Abs(p2.X - p1.X), Math.Abs(p2.Y - p1.Y))))
            {
                Canvas.DrawLine((float)p1.X, (float)p1.Y, (float)p2.X, (float)p2.Y, paint.Paint);
            }
        }

        public void DrawGeometry(IBrush brush, Pen pen, Geometry geometry)
        {
            var impl = ((StreamGeometryImpl)geometry.PlatformImpl);
            var size = geometry.Bounds.Size;

            using (var fill = brush != null ? CreatePaint(brush, size) : default(PaintWrapper))
            using (var stroke = pen?.Brush != null ? CreatePaint(pen, size) : default(PaintWrapper))
            {
                if (fill.Paint != null)
                {
                    Canvas.DrawPath(impl.EffectivePath, fill.Paint);
                }
                if (stroke.Paint != null)
                {
                    Canvas.DrawPath(impl.EffectivePath, stroke.Paint);
                }
            }
        }

        private struct PaintDisposable : IDisposable
        {
            private Action _action;

            public PaintDisposable(Action action)
            {
                _action = action;
            }

            public void Dispose()
            {
                _action?.Invoke();
            }
        }

        internal struct PaintWrapper : IDisposable
        {
            //We are saving memory allocations there
            //TODO: add more disposable fields if needed
            public readonly SKPaint Paint;

            private IDisposable _disposable1;

            private Func<SKPaint, SKPaint, IDisposable> _applyToAction;

            public void SetApplyToAction<T>(Func<SKPaint, T> get,
                                            Action<SKPaint, SKPaint> apply,
                                            Action<SKPaint, T> revert)
            {
                _applyToAction = (from, to) =>
                {
                    T state = get(to);
                    apply(from, to);
                    return new PaintDisposable(() => revert(to, state));
                };
            }

            public IDisposable ApplyTo(SKPaint paint)
            {
                return _applyToAction != null ? _applyToAction(Paint, paint) : default(PaintDisposable);
            }

            public void AddDisposable(IDisposable disposable)
            {
                if (_disposable1 == null)
                    _disposable1 = disposable;
                else
                    throw new InvalidOperationException();
            }

            public PaintWrapper(SKPaint paint)
            {
                Paint = paint;
                _disposable1 = null;
                _applyToAction = null;
            }

            public void Dispose()
            {
                Paint?.Dispose();
                _disposable1?.Dispose();
            }
        }

        private PaintWrapper CreatePaint(IBrush brush, Size targetSize)
        {
            SKPaint paint = new SKPaint();
            var rv = new PaintWrapper(paint);
            paint.IsStroke = false;

            // TODO: SkiaSharp does not contain alpha yet!
            double opacity = brush.Opacity * _currentOpacity;
            //paint.SetAlpha(paint.GetAlpha() * opacity);
            paint.IsAntialias = true;

            SKColor color = new SKColor(255, 255, 255, 255);

            var solid = brush as ISolidColorBrush;
            if (solid != null)
                color = solid.Color.ToSKColor();

            paint.Color = (new SKColor(color.Red, color.Green, color.Blue, (byte)(color.Alpha * opacity)));

            if (solid != null)
            {
                rv.SetApplyToAction(p => p.Color,
                                    (from, to) => to.Color = from.Color,
                                    (p, v) => p.Color = v);
                return rv;
            }

            var gradient = brush as GradientBrush;
            if (gradient != null)
            {
                var tileMode = gradient.SpreadMethod.ToSKShaderTileMode();
                var stopColors = gradient.GradientStops.Select(s => s.Color.ToSKColor()).ToArray();
                var stopOffsets = gradient.GradientStops.Select(s => (float)s.Offset).ToArray();

                var linearGradient = brush as LinearGradientBrush;
                if (linearGradient != null)
                {
                    var start = linearGradient.StartPoint.ToPixels(targetSize).ToSKPoint();
                    var end = linearGradient.EndPoint.ToPixels(targetSize).ToSKPoint();

                    // would be nice to cache these shaders possibly?
                    var shader = SKShader.CreateLinearGradient(start, end, stopColors, stopOffsets, tileMode);
                    paint.Shader = shader;
                    shader.Dispose();
                }
                else
                {
                    var radialGradient = brush as RadialGradientBrush;
                    if (radialGradient != null)
                    {
                        var center = radialGradient.Center.ToPixels(targetSize).ToSKPoint();
                        var radius = (float)radialGradient.Radius;

                        // TODO: There is no SetAlpha in SkiaSharp
                        //paint.setAlpha(128);

                        // would be nice to cache these shaders possibly?
                        var shader = SKShader.CreateRadialGradient(center, radius, stopColors, stopOffsets, tileMode);
                        paint.Shader = shader;
                        shader.Dispose();
                    }
                }

                rv.SetApplyToAction(p => new Tuple<SKColor, SKShader>(p.Color, p.Shader),
                                    (from, to) => { to.Color = from.Color; to.Shader = from.Shader; },
                                    (p, v) => { p.Color = v.Item1; p.Shader = v.Item2; });
                return rv;
            }

            var tileBrush = brush as TileBrush;
            if (tileBrush != null)
            {
                var helper = new TileBrushImplHelper(tileBrush, targetSize);
                var bitmap = new BitmapImpl((int)helper.IntermediateSize.Width, (int)helper.IntermediateSize.Height);
                rv.AddDisposable(bitmap);
                using (var ctx = bitmap.CreateDrawingContext())
                    helper.DrawIntermediate(ctx);
                SKMatrix translation = SKMatrix.MakeTranslation(-(float)helper.DestinationRect.X, -(float)helper.DestinationRect.Y);
                SKShaderTileMode tileX =
                    tileBrush.TileMode == TileMode.None
                        ? SKShaderTileMode.Clamp
                        : tileBrush.TileMode == TileMode.FlipX || tileBrush.TileMode == TileMode.FlipXY
                            ? SKShaderTileMode.Mirror
                            : SKShaderTileMode.Repeat;

                SKShaderTileMode tileY =
                    tileBrush.TileMode == TileMode.None
                        ? SKShaderTileMode.Clamp
                        : tileBrush.TileMode == TileMode.FlipY || tileBrush.TileMode == TileMode.FlipXY
                            ? SKShaderTileMode.Mirror
                            : SKShaderTileMode.Repeat;
                paint.Shader = SKShader.CreateBitmap(bitmap.Bitmap, tileX, tileY, translation);
                paint.Shader.Dispose();

                rv.SetApplyToAction(p => new Tuple<SKColor, SKShader>(p.Color, p.Shader),
                                    (from, to) => { to.Color = from.Color; to.Shader = from.Shader; },
                                    (p, v) => { p.Color = v.Item1; p.Shader = v.Item2; });
            }

            return rv;
        }

        private PaintWrapper CreatePaint(Pen pen, Size targetSize)
        {
            var rv = CreatePaint(pen.Brush, targetSize);
            var paint = rv.Paint;

            paint.IsStroke = true;
            paint.StrokeWidth = (float)pen.Thickness;

            if (pen.StartLineCap == PenLineCap.Round)
                paint.StrokeCap = SKStrokeCap.Round;
            else if (pen.StartLineCap == PenLineCap.Square)
                paint.StrokeCap = SKStrokeCap.Square;
            else
                paint.StrokeCap = SKStrokeCap.Butt;

            if (pen.LineJoin == PenLineJoin.Miter)
                paint.StrokeJoin = SKStrokeJoin.Mitter;
            else if (pen.LineJoin == PenLineJoin.Round)
                paint.StrokeJoin = SKStrokeJoin.Round;
            else
                paint.StrokeJoin = SKStrokeJoin.Bevel;

            paint.StrokeMiter = (float)pen.MiterLimit;

            // TODO: Implement Dash Style support
            //
            //if (pen.DashStyle?.Dashes != null)
            //{
            //	var dashes = pen.DashStyle.Dashes;
            //	if (dashes.Count > NativeBrush.MaxDashCount)
            //		throw new NotSupportedException("Maximum supported dash count is " + NativeBrush.MaxDashCount);
            //	brush.Brush->StrokeDashCount = dashes.Count;
            //	for (int c = 0; c < dashes.Count; c++)
            //		brush.Brush->StrokeDashes[c] = (float)dashes[c];
            //	brush.Brush->StrokeDashOffset = (float)pen.DashStyle.Offset;

            //}

            //if (brush->StrokeDashCount != 0)
            //{
            //	paint.setPathEffect(SkDashPathEffect::Create(brush->StrokeDashes, brush->StrokeDashCount, brush->StrokeDashOffset))->unref();
            //}

            return rv;
        }

        public void DrawRectangle(Pen pen, Rect rect, float cornerRadius = 0)
        {
            using (var paint = CreatePaint(pen, rect.Size))
            {
                var rc = rect.ToSKRect();
                if (cornerRadius == 0)
                {
                    Canvas.DrawRect(rc, paint.Paint);
                }
                else
                {
                    Canvas.DrawRoundRect(rc, cornerRadius, cornerRadius, paint.Paint);
                }
            }
        }

        public void FillRectangle(IBrush brush, Rect rect, float cornerRadius = 0)
        {
            using (var paint = CreatePaint(brush, rect.Size))
            {
                var rc = rect.ToSKRect();
                if (cornerRadius == 0)
                {
                    Canvas.DrawRect(rc, paint.Paint);
                }
                else
                {
                    Canvas.DrawRoundRect(rc, cornerRadius, cornerRadius, paint.Paint);
                }
            }
        }

        public void DrawText(IBrush foreground, Point origin, FormattedText text)
        {
            using (var paint = CreatePaint(foreground, text.Measure()))
            {
                var textImpl = text.PlatformImpl as FormattedTextImpl;
                textImpl.Draw(Canvas, origin.ToSKPoint(), paint, CreatePaint);
            }
        }

        public void PushClip(Rect clip)
        {
            Canvas.Save();
            Canvas.ClipRect(clip.ToSKRect());
        }

        public void PopClip()
        {
            Canvas.Restore();
        }

        private double _currentOpacity = 1.0f;
        private readonly Stack<double> _opacityStack = new Stack<double>();

        public void PushOpacity(double opacity)
        {
            _opacityStack.Push(_currentOpacity);
            _currentOpacity *= opacity;
        }

        public void PopOpacity()
        {
            _currentOpacity = _opacityStack.Pop();
        }

        public virtual void Dispose()
        {
        }

        public void PushGeometryClip(Geometry clip)
        {
            Canvas.Save();
            Canvas.ClipPath(((StreamGeometryImpl)clip.PlatformImpl).EffectivePath);
        }

        public void PopGeometryClip()
        {
            Canvas.Restore();
        }

        private Matrix _currentTransform = Matrix.Identity;

        public Matrix Transform
        {
            get { return _currentTransform; }
            set
            {
                if (_currentTransform == value)
                    return;

                _currentTransform = value;
                Canvas.SetMatrix(value.ToSKMatrix());
            }
        }
    }
}