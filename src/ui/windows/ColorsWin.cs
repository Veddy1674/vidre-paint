using Silk.NET.Input;
using SkiaSharp;
using Vidre.src.input;
using Vidre.src.UI.components;

namespace Vidre.src.UI.windows;

class ColorsWin(SKRectI screen, ToolManager toolManager) : FloatingWin(screen,
    "Colors",
    x: 15,
    y: screen.Height - 15 - 200,
    width: 360,
    height: 200
)
{
    private readonly ToolManager toolManager = toolManager; // make it accessible to children classes

    private static readonly SKBitmap arrow1 = Utils.LoadImage("arrow1.png"); // used in the swap colors squares

    private UIComponent[] UIComponents = [];

    public bool editingPrimaryColor = true; // whether primary or secondary color is being edited (shared across all components)

    public override void Init(SKRect _screen, SKPaint _paint)
    {
        // init here components (so that "this" is allowed)
        var allSliders = new AllSliders(this.toolManager, this);

        UIComponents = [
            // order matters for priority

            new ColorsSwap(this),
            new QuickColors(this.toolManager),
            new RGBColorWheel(this.toolManager, allSliders),
            allSliders,
        ];

        foreach (var ui in UIComponents)
            ui.Init(_screen, _paint);
    }

    private bool firstDraw = true;

    protected override void DrawContent(SKCanvas r, SKRect win, bool windowMoved, double deltaTime, SKPaint paint)
    {
        if (windowMoved || firstDraw)
        {
            firstDraw = false;

            foreach (var ui in UIComponents)
                ui.Compute(win);
        }

        foreach (var ui in UIComponents)
            ui.Draw(r, deltaTime, paint);
    }

    #region ALL the UI components:

    // the two squares that show primary and secondary color
    private class ColorsSwap(ColorsWin parent) : UIComponent
    {
        private readonly ToolManager toolManager = parent.toolManager;
        private readonly ColorsWin parent = parent;

        private SKRect hitbox; // "hitbox" to swap colors
        private SKRect backSquare; // secondary
        private SKRect frontSquare; // primary

        private SKShader transparencyShader = null!;

        public override void Init(SKRect _screen, SKPaint _paint)
        {
            // init transparency shader (like in Canvas.cs)
            using var tempBitmap = new SKBitmap(24, 24);
            using var tempCanvas = new SKCanvas(tempBitmap);

            tempCanvas.Clear(SKColors.White);
            using var paint = new SKPaint { Color = new SKColor(180, 180, 180) };

            tempCanvas.DrawRect(0, 0, 12, 12, paint);
            tempCanvas.DrawRect(12, 12, 12, 12, paint);

            transparencyShader = SKShader.CreateBitmap(tempBitmap, SKShaderTileMode.Repeat, SKShaderTileMode.Repeat);
        }

        public override void Compute(SKRect win)
        {
            float baseX = win.Left + 24;
            float baseY = win.Top + 26;

            backSquare = SKRect.Create(baseX, baseY, 24, 24);
            frontSquare = SKRect.Create(baseX - 10, baseY - 10, 24, 24);

            hitbox = new(frontSquare.Left, frontSquare.Top, backSquare.Right, backSquare.Bottom); // includes perfectly both squares (centered)
            hitbox.Inflate(8, 8);
        }

        public override void Draw(SKCanvas r, double deltaTime, SKPaint paint)
        {
            // hitbox.DrawDebug(r, paint);

            //which color is currently being edited
            var backColor = parent.editingPrimaryColor ? toolManager.SecondaryColor : toolManager.PrimaryColor;
            var frontColor = parent.editingPrimaryColor ? toolManager.PrimaryColor : toolManager.SecondaryColor;

            // square behind
            float left = backSquare.Left;
            float top = backSquare.Top;

            paint.Color = backColor;

            // make transparencyShader relative so it stays still
            paint.Shader = transparencyShader.WithLocalMatrix(SKMatrix.CreateTranslation(left, top));
            r.DrawRoundRect(backSquare, 4, 4, paint);

            paint.Shader = null;
            r.DrawRoundRect(backSquare, 4, 4, paint);

            // square front (currently editing color)
            left = backSquare.Left + 2;
            top = backSquare.Top + 2;

            paint.Color = frontColor;

            paint.BlendMode = SKBlendMode.Src; // no blend with background
            paint.Shader = transparencyShader.WithLocalMatrix(SKMatrix.CreateTranslation(left, top));
            r.DrawRoundRect(frontSquare, 4, 4, paint);

            paint.BlendMode = SKBlendMode.SrcOver; // blend with transparency rect
            paint.Shader = null;
            r.DrawRoundRect(frontSquare, 4, 4, paint);

            // little arrow
            r.DrawBitmap(arrow1, backSquare.Left + 16, backSquare.Top - 10);
        }

        public override bool OnMouseDown(bool leftDown, bool rightDown, SKPoint mousePos)
        {
            if (hitbox.Contains(mousePos))
            {
                toolManager.SwapColors();
                return true;
            }
            
            return false;
        }
    }

    // the many little squares (palette)
    private class QuickColors(ToolManager toolManager) : UIComponent
    {
        // quick colors:
        private static readonly SKColor[] quickColors = [
            // pure black, pure white
            new(0, 0, 0), new(255, 255, 255),

            // dark gray, light gray
            new(40, 40, 40), new(120, 120, 120),

            // red, orange, yellow
            new(200, 40, 20), new(255, 100, 50), new(220, 210, 50),

            // dark green, dark cyan, cyan
            new(0, 100, 0), new(34, 139, 34), new(125, 210, 125),

            // deep blue, blue, light blue
            new(0, 60, 140), new(40, 110, 210), new(80, 140, 220), new(120, 180, 250),

            // dark purple, purple, light purple
            new(60, 20, 150), new(110, 50, 200), new(160, 110, 235), new(190, 120, 255),
        ];

        private readonly SKRect[] quickColorsRect = new SKRect[quickColors.Length];
        private readonly int[] colsPerRow = [2, 2, 3, 3, 4, 4]; // NOTE: sum of these must equal to quickColors.Length

        const float squaresDistance = 16f; // distance of each little square to eachother
        const float squaresSize = 12f; // size of each little square
        const float hitboxOffset = 4f; // tiny offset for better hover feeling (larger hitbox but visual remains the same)
        const float hitboxOffsetHalf = -hitboxOffset / 2; // reverse (to remove the offset)

        private readonly float[] xOffsets = [0, squaresDistance / 2];

        public override void Compute(SKRect win)
        {
            float baseX = win.Left + 8;
            float baseY = win.Top + 64; // below the two squares

            int row = 0, col = 0;

            for (int i = 0; i < quickColors.Length; i++)
            {
                float offsetX = baseX + col * squaresDistance + xOffsets[row % 2];
                float offsetY = baseY + row * squaresDistance;
                
                quickColorsRect[i] = SKRect.Create(offsetX, offsetY, squaresSize + hitboxOffset, squaresSize + hitboxOffset);
                
                col++;
                if (col >= colsPerRow[row])
                {
                    col = 0;
                    row++;
                }
            }
        }

        private int selectedQCIndex = -1;

        public override void Draw(SKCanvas r, double deltaTime, SKPaint paint)
        {
            // shared i between two arrays
            for (int i = 0; i < quickColors.Length; i++)
            {
                var rect = quickColorsRect[i];
                rect.Inflate(hitboxOffsetHalf, hitboxOffsetHalf); // remove tiny offset

                // if selected, make rect bigger
                if (selectedQCIndex == i)
                    rect.Inflate(1, 1);

                paint.Color = quickColors[i];
                r.DrawRect(rect, paint);
            }
        }

        public override bool OnMouseDown(bool leftDown, bool rightDown, SKPoint mousePos)
        {
            if (selectedQCIndex == -1) return false;

            var color = quickColors[selectedQCIndex];

            if (leftDown) // priority
            {
                toolManager.SetPrimaryColor(color);
            }
            else if (rightDown)
                toolManager.SetSecondaryColor(color);

            return true;
        }

        public override bool OnMouseMove(bool leftDown, bool rightDown, SKPoint lastMousePos, SKPoint mousePos)
        {
            selectedQCIndex = -1;

            for (int i = 0; i < quickColors.Length; i++)
            {
                if (quickColorsRect[i].Contains(mousePos))
                {
                    selectedQCIndex = i;
                    // break;
                    return true; // so that windows behind don't process hover animations
                }
            }

            return false;
        }
    }

    // RGB color wheel to select hue
    private class RGBColorWheel(ToolManager toolManager, AllSliders allSliders) : UIComponent
    {
        private SKRect WheelRect;
        private SKPoint WheelCenter;
        private float wheelOffsetX = 0, wheelOffsetY = 0;

        private const int wheelRadius = 64;
        private const int wheelRadius2 = 64 * 64;
        private readonly SKBitmap wheelBmp = CreateColorWheel(wheelRadius * 2); // diameter = 64x2

        private bool draggingRing = false;

        // color wheel gradient colors (same order as paint.net)
        public static readonly SKColor[] wheelColors = [
            SKColors.Red, SKColors.Yellow, SKColors.Lime, SKColors.Cyan,
            SKColors.Blue, SKColors.Fuchsia, SKColors.Red,
        ];

        public override void Init(SKRect screen, SKPaint paint)
        {
            SetWheelFromColor(toolManager.PrimaryColor);

            toolManager.OnPrimaryColorChanged += () => {
                // NOTE: only call when primary color was changed from outside this component!
                if (!updatingFromWheel && !allSliders.UpdatingValueOrAlpha)
                    SetWheelFromColor(toolManager.PrimaryColor);
            };

            toolManager.OnSecondaryColorChanged += () => {
                // NOTE: only call when secondary color was changed from outside this component!
                if (!updatingFromWheel && !allSliders.UpdatingValueOrAlpha)
                    SetWheelFromColor(toolManager.SecondaryColor);
            };
        }

        public override void Compute(SKRect win)
        {
            float baseX = win.Left + 64;
            float baseY = win.Top + 8;

            WheelRect = SKRect.Create(baseX, baseY, wheelBmp.Width, wheelBmp.Height);
            WheelCenter = new(WheelRect.MidX, WheelRect.MidY);
        }

        public override void Draw(SKCanvas r, double deltaTime, SKPaint paint)
        {
            // draw circle bitmap
            r.DrawCircle(WheelCenter, wheelRadius + 2, Utils.ShadowPaint);
            r.DrawBitmap(wheelBmp, WheelRect);

            // draw little ring
            paint.StrokeWidth = 2f;
            paint.IsAntialias = true;
            paint.Color = SKColors.DarkSlateGray;
            paint.Style = SKPaintStyle.Stroke;
            
            var mousePosInWheel = new SKPoint(WheelCenter.X + wheelOffsetX, WheelCenter.Y + wheelOffsetY);

            r.DrawCircle(mousePosInWheel, 4, paint);
            paint.Reset();
        }

        public override bool OnMouseDown(bool leftDown, bool rightDown, SKPoint mousePos)
        {
            if ((leftDown || rightDown) && IsPosInWheel(mousePos))
            {
                draggingRing = true;

                wheelOffsetX = mousePos.X - WheelCenter.X;
                wheelOffsetY = mousePos.Y - WheelCenter.Y;

                // clamp to circle radius
                float distanceSquared = wheelOffsetX * wheelOffsetX + wheelOffsetY * wheelOffsetY;
                if (distanceSquared > wheelRadius2)
                {
                    float distance = (float)Math.Sqrt(distanceSquared);
                    wheelOffsetX = wheelOffsetX * wheelRadius / distance;
                    wheelOffsetY = wheelOffsetY * wheelRadius / distance;
                }

                SetColorFromWheel(leftDown); // priority to left
                return true;
            }
            return false;
        }

        public override bool OnMouseUp(bool leftDown, bool rightDown, SKPoint mousePos)
        {
            if (!leftDown && draggingRing)
            {
                draggingRing = false;
                return true;
            }
            return false;
        }

        public override bool OnMouseMove(bool leftDown, bool rightDown, SKPoint lastMousePos, SKPoint mousePos)
        {
            if (draggingRing)
            {
                float newOffsetX = mousePos.X - WheelCenter.X;
                float newOffsetY = mousePos.Y - WheelCenter.Y;
                
                // smooth clamp to circle radius
                float distanceSquared = newOffsetX * newOffsetX + newOffsetY * newOffsetY;
                if (distanceSquared > wheelRadius2)
                {
                    float distance = (float)Math.Sqrt(distanceSquared);
                    newOffsetX = newOffsetX * wheelRadius / distance;
                    newOffsetY = newOffsetY * wheelRadius / distance;
                }
                
                wheelOffsetX = newOffsetX;
                wheelOffsetY = newOffsetY;

                SetColorFromWheel(leftDown); // priority to left
                return true;
            }
            return false;
        }

        private bool IsPosInWheel(SKPoint pos)
        {
            float dx = pos.X - WheelCenter.X;
            float dy = pos.Y - WheelCenter.Y;
            return (dx * dx + dy * dy) <= wheelRadius2;
        }

        private bool updatingFromWheel = false;

        private void SetColorFromWheel(bool leftOrRight)
        {
            var color = leftOrRight ? toolManager.PrimaryColor : toolManager.SecondaryColor;

            // get color based on wheel offsets
            float dx = wheelOffsetX / wheelRadius;
            float dy = wheelOffsetY / wheelRadius;

            float sat = Math.Clamp((float)Math.Sqrt(dx * dx + dy * dy), 0, 1) * 100;
            float hue = (float)(Math.Atan2(dy, dx) * 180 / Math.PI + 360) % 360;

            color.ToHsv(out float _, out float _, out float val);
            // clamp to avoid crashes (e.g: moving saturation slider and then value slider)
            var newColor = SKColor.FromHsv(Math.Clamp(hue, 0, 360), Math.Clamp(sat, 0, 100), Math.Clamp(val, 0, 100), color.Alpha); // keep value and alpha

            updatingFromWheel = true;

            // invoke event without calling SetWheelFromColor again (to avoid loop)
            if (leftOrRight)
                toolManager.SetPrimaryColor(newColor);
            else
                toolManager.SetSecondaryColor(newColor);
            
            updatingFromWheel = false;
        }

        // NOTE: shouldn't be called if value or alpha was changed
        private void SetWheelFromColor(SKColor color)
        {
            color.ToHsv(out float hue, out float sat, out float _);

            // round to 2 decimal places for consistency
            hue = MathF.Round(hue, 2);
            sat = MathF.Round(sat, 2);

            float rad = hue * MathF.PI / 180f;
            wheelOffsetX = MathF.Cos(rad) * sat / 100f * wheelRadius;
            wheelOffsetY = MathF.Sin(rad) * sat / 100f * wheelRadius;
        }

        private static SKBitmap CreateColorWheel(int size)
        {
            var bmp = new SKBitmap(size, size);
            using var canvas = new SKCanvas(bmp);
            canvas.Clear(SKColors.Transparent);

            var center = new SKPoint(size / 2f, size / 2f);
            var radius = size / 2f;

            using var p = new SKPaint { IsAntialias = true };
            
            // base hue gradient
            p.Shader = SKShader.CreateSweepGradient(center, wheelColors, null);
            canvas.DrawCircle(center, radius, p);

            // saturation gradient (white to transparent)
            p.Shader = SKShader.CreateRadialGradient(center, radius, 
                [SKColors.White, SKColors.White.WithAlpha(0)], [0f, 1f], SKShaderTileMode.Clamp);
            
            canvas.DrawCircle(center, radius, p);

            return bmp;
        }
    }

    // RGB, HSV and alpha sliders
    private class AllSliders(ToolManager toolManager, ColorsWin parent) : UIComponent
    {
        private readonly ColorsWin parent = parent;

        // defined in Init
        private readonly SKRect[] slidersRect = new SKRect[4];
        private readonly SKRect[] slidersInputRect = new SKRect[4]; // input box next to each slider
        private readonly UITextInput[] sliderTextInputs = new UITextInput[4]; // the text input boxes of input rects
        private UITextInput? sliderTextInputFocused = null; // the focused UITextInput

        private SKRect separator; // separates first 3 sliders from alpha
        private SKRect switchRGBRect; // to switch to RGB
        private SKRect switchHSVRect; // to switch to HSV

        private string HexCode => $"{AmountR:X2}{AmountG:X2}{AmountB:X2}"; // TODO: optimize?
        private HexInput hexInputUI = null!; // hex code of color

        // these amounts are pretty much a shortcut to toolManager.PrimaryColor/SecondaryColor
        private byte AmountR, AmountG, AmountB, AmountA; // 0-255

        // float because they have different ranges (0-360, 0-100, 0-100)
        private float AmountH, AmountS, AmountV;

        private bool RGBMode = false; // whether to use RGB or HSV (first 3 sliders)

        private readonly SKPath triangleHandle = new(); // to draw the little triangle handle on each slider

        private SKShader transparencyShader = null!;

        public override void Init(SKRect _screen, SKPaint _paint)
        {
            // init transparency shader (like in Canvas.cs)
            using var tempBitmap = new SKBitmap(12, 12);
            using var tempCanvas = new SKCanvas(tempBitmap);

            tempCanvas.Clear(SKColors.White);
            using var paint = new SKPaint { Color = new SKColor(180, 180, 180) };

            tempCanvas.DrawRect(0, 0, 6, 6, paint);
            tempCanvas.DrawRect(6, 6, 6, 6, paint);

            transparencyShader = SKShader.CreateBitmap(tempBitmap, SKShaderTileMode.Repeat, SKShaderTileMode.Repeat);

            // init hex code
            hexInputUI = new(this); // updated the first time below, in UpdateFromColor!

            // init triangle handle
            const float triangleSize = 5f;

            triangleHandle.MoveTo(0, 0);
            triangleHandle.LineTo(triangleSize, triangleSize * 2);
            triangleHandle.LineTo(-triangleSize, triangleSize * 2);
            triangleHandle.Close();

            #region Init sliderTextInputs
            // setup sliders input box
            for (int i = 0; i < sliderTextInputs.Length; i++)
            {
                // updated the first time below, in UpdateFromColor!
                sliderTextInputs[i] = new UITextInput(fontSize: 12f, initText: "0", maxTextLength: 3, textAlignment: TextAlignment.Center)
                {
                    // only allow numbers
                    KeyTypeCondition = (c) => c >= '0' && c <= '9',
                    // set to 0 when all chars are deleted (minimum value for all sliders)
                    OnDeleteAll = (_) => "0"
                };
            }
            
            // set specific actions for each slider input box:
            var s1 = sliderTextInputs[0];
            var s2 = sliderTextInputs[1];
            var s3 = sliderTextInputs[2];
            var s4 = sliderTextInputs[3];

            UpdateSliderTextInputsEvents(s1, s2, s3, s4);

            // OnKeyCharAction and OnDeleteAction and OnFocusLost (adjust string and update color)
            {
                s1.OnKeyCharAction = s1.OnDeleteAction = s1.OnFocusLostAction = sliderInput1_AdjustAndUpdateColor;
                s2.OnKeyCharAction = s2.OnDeleteAction = s2.OnFocusLostAction = sliderInput2_AdjustAndUpdateColor;
                s3.OnKeyCharAction = s3.OnDeleteAction = s3.OnFocusLostAction = sliderInput3_AdjustAndUpdateColor;
                s4.OnKeyCharAction = s4.OnDeleteAction = s4.OnFocusLostAction = sliderInput4_AdjustAndUpdateColor;
            }
            // OnTabPress (switch to next input box)
            {
                s1.OnTabPress = () =>
                {
                    s1.OnFocusLost();
                    sliderTextInputFocused = s2;
                    s2.GainFocus();
                };

                s2.OnTabPress = () =>
                {
                    s2.OnFocusLost();
                    sliderTextInputFocused = s3;
                    s3.GainFocus();
                };

                s3.OnTabPress = () =>
                {
                    s3.OnFocusLost();
                    sliderTextInputFocused = s4;
                    s4.GainFocus();
                };

                s4.OnTabPress = () =>
                {
                    s4.OnFocusLost();
                    sliderTextInputFocused = s1;
                    s1.GainFocus();
                };
            }
            #endregion

            // init sliders and events
            UpdateFromColor(toolManager.PrimaryColor); // NOTE: this updates all amounts!

            toolManager.OnPrimaryColorChanged += () =>
            {
                parent.editingPrimaryColor = true;
                UpdateFromColor(toolManager.PrimaryColor);
            };
            toolManager.OnSecondaryColorChanged += () =>
            {
                parent.editingPrimaryColor = false;
                UpdateFromColor(toolManager.SecondaryColor);
            };
        }

        #region private utils related to sliderTextInputs
        // e.g: update OnTextExceed when RGBMode is changes, because RGB and HSV have different max values
        private void UpdateSliderTextInputsEvents(UITextInput s1, UITextInput s2, UITextInput s3, UITextInput s4)
        {
            // OnTextExceed (set values to max)
            {
                s1.OnTextExceed = () =>
                {
                    // force current text to max value
                    s1.SetText(RGBMode ? "255" : "360", loseFocus: false); // R or H

                    sliderInput1_AdjustAndUpdateColor();
                };
                
                s2.OnTextExceed = () =>
                {
                    // force current text to max value
                    s2.SetText(RGBMode ? "255" : "100", loseFocus: false); // G or S

                    sliderInput2_AdjustAndUpdateColor();
                };
                
                s3.OnTextExceed = () =>
                {
                    // force current text to max value
                    s3.SetText(RGBMode ? "255" : "100", loseFocus: false); // B or V

                    sliderInput3_AdjustAndUpdateColor();
                };

                s4.OnTextExceed = () =>
                {
                    // force current text to max value
                    s4.SetText("255", loseFocus: false); // A

                    sliderInput4_AdjustAndUpdateColor();
                };
            }
        }
        
        // (to avoid code repetition) only used in methods below
        private static float general_AdjustText(ref UITextInput s, string maxIndex)
        {
            // delete zeros on the left (e.g: 05 -> 5)
            // to do it, simply convert to int and back to string so it auto-adjusts
            int textInt = int.Parse(s.CurrText);

            // if textInt > max then set it to max
            var max = TextLettersMaxValue[maxIndex];

                if (textInt > max)
                {
                    s.SetText(max.ToString(), loseFocus: false);
                    return max;
                }
            else // convert back to string (for the 05 -> 5 thing)
                    s.SetText(textInt.ToString(), loseFocus: false);

            return (float)textInt;
        }

        // what to do when a slider text input is changed
        private void sliderInput1_AdjustAndUpdateColor()
        {
            var textFloat = general_AdjustText(ref sliderTextInputs[0], RGBMode ? "R" : "H");
            
            // update amounts and color
            if (RGBMode)
                AmountR = (byte)textFloat;
            else
                AmountH = textFloat;

            UpdateColorFromAmounts();
        }

        private void sliderInput2_AdjustAndUpdateColor()
        {
            var textFloat = general_AdjustText(ref sliderTextInputs[1], RGBMode ? "G" : "S");
            
            // update amounts and color
            if (RGBMode)
                AmountG = (byte)textFloat;
            else
                AmountS = textFloat;

            UpdateColorFromAmounts();
        }

        private void sliderInput3_AdjustAndUpdateColor()
        {
            var textFloat = general_AdjustText(ref sliderTextInputs[2], RGBMode ? "B" : "V");
            
            // update amounts and color
            if (RGBMode)
                AmountB = (byte)textFloat;
            else
                AmountV = textFloat;

            UpdateColorFromAmounts();
        }

        private void sliderInput4_AdjustAndUpdateColor()
        {
            var textFloat = general_AdjustText(ref sliderTextInputs[3], "A");

            // update amounts and color
            AmountA = (byte)textFloat;
            UpdateColorFromAmounts();
        }
        #endregion

        public override void Compute(SKRect win)
        {
            float baseX = win.Left + 218;
            float baseY = win.Top + 12;

            const float height = 18f;
            const float width = 100f;
            const float yDist = 30f;

            slidersRect[0] = SKRect.Create(baseX, baseY, width, height); // Red or Hue
            slidersRect[1] = SKRect.Create(baseX, baseY += yDist, width, height); // Green or Saturation
            slidersRect[2] = SKRect.Create(baseX, baseY += yDist, width, height); // Blue or Value

            separator = SKRect.Create(baseX - 16, slidersRect[2].Bottom + 15, 148, 2);

            // Alpha
            slidersRect[3] = SKRect.Create(baseX, separator.Bottom + 13, width, height); // +13 to account for the separator height

            baseY = slidersRect[3].Top + 28; // adjust

            // input boxes of sliders
            for (int i = 0; i < slidersInputRect.Length; i++)
                // update slidersInputRect and then sliderUITextInputs
                sliderTextInputs[i].UpdateContainer(
                    slidersInputRect[i] = SKRect.Create(slidersRect[i].Right + 6, slidersRect[i].Top - 3, 26, 24)
                );

            // hex input box
            var hexInputRect = SKRect.Create(baseX + width - 81, baseY, 81, 20);
            hexInputUI.Update(hexInputRect);

            // switch mode buttons
            switchRGBRect = SKRect.Create(hexInputRect.Left - 99 - 52, baseY, 52, 16);
            switchHSVRect = SKRect.Create(switchRGBRect.Right + 4, baseY, 52, 16);

            // this method is called after init and then everytime the window moves
            // so the sliders are updated here
            UpdateShaders();
        }

        public override void Draw(SKCanvas r, double deltaTime, SKPaint paint)
        {
            // (optimized rather than 12 ternary conditions e.g: RGBMode ? "R" : "H")
            if (RGBMode)
            {
                DrawSlider(r, paint, 0, "R", AmountR, AmountR.ToString("0"), deltaTime);
                DrawSlider(r, paint, 1, "G", AmountG, AmountG.ToString("0"), deltaTime);
                DrawSlider(r, paint, 2, "B", AmountB, AmountB.ToString("0"), deltaTime);
            } else
            {
                DrawSlider(r, paint, 0, "H", AmountH, AmountH.ToString("0"), deltaTime);
                DrawSlider(r, paint, 1, "S", AmountS, AmountS.ToString("0"), deltaTime);
                DrawSlider(r, paint, 2, "V", AmountV, AmountV.ToString("0"), deltaTime);
            }
            
            // draw alpha always
            DrawSlider(r, paint, 3, "A", AmountA, AmountA.ToString("0"), deltaTime);

            // draw separator line
            paint.Color = SKColors.DarkGray;
            r.DrawRect(separator, paint);

            // draw hex input
            hexInputUI.Draw(r, deltaTime, paint);

            // draw RGB switch
            DrawSwitchButton(r, paint, ref switchRGBRect, true);

            // draw HSV switch
            DrawSwitchButton(r, paint, ref switchHSVRect, false);
        }

        private void DrawSwitchButton(SKCanvas r, SKPaint paint, ref SKRect switchRect, bool rgb)
        {
            paint.IsAntialias = true;

            // draw switch mode button
            paint.Color = shadowColor;
            switchRect.Offset(0, 2);
            r.DrawRoundRect(switchRect, 4, 4, paint);

            // TODO: change buttons, they look bad
            paint.Color = (rgb == RGBMode) ? SKColors.Gray : SKColors.DarkGray;
            switchRect.Offset(0, -2);
            r.DrawRoundRect(switchRect, 4, 4, paint);

            paint.IsAntialias = false;

            // draw RGB switch mode button text
            UIManager.MainTextFont.Size = 14f;
            r.DrawText(rgb ? "RGB" : "HSV", switchRect.MidX, switchRect.MidY + 5, SKTextAlign.Center, UIManager.MainTextFont, UIManager.MainTextPaint);
        }

        private void DrawSlider(SKCanvas r, SKPaint paint, int sliderIdx, string textLetter, float currValue, string currValueString, double deltaTime)
        {
            var sliderRect = slidersRect[sliderIdx];
            var wholeInputRect = slidersInputRect[sliderIdx];
            var inputRectText = sliderTextInputs[sliderIdx];

            // draw textLetter (acceptably accurate offsets)
            UIManager.MainTextFont.Size = 14f;
            r.DrawText(textLetter, sliderRect.Left - 16, sliderRect.MidY + 5, SKTextAlign.Left, UIManager.MainTextFont, UIManager.MainTextPaint);

            paint.IsAntialias = true;

            // draw shadow of input box and slider
            paint.Color = shadowColor;

            wholeInputRect.DrawShadowRect(r, paint, 4, 4, offset: 2f);
            sliderRect.DrawShadowRect(r, paint, 0, 0, offset: 2f);

            // draw input box
            paint.Color = inputBoxColor;
            r.DrawRoundRect(wholeInputRect, 4, 4, paint);

            paint.IsAntialias = false;

            // draw value in input box
            inputRectText.Draw(r, deltaTime, paint);

            // draw slider rect
            paint.Color = SKColors.White; // important! avoid shader from looking dark from previous colors
            // mainly from inputRectText, which changes color to Config.TextSelection...

            paint.Shader = GetShaderOf(textLetter, sliderRect);

            r.DrawRect(sliderRect, paint);
            paint.Shader = null;

            // setup triangle handle
            float cursorX = sliderRect.Left + (sliderRect.Width * GetPercentOf(currValue, textLetter));
            float cursorY = sliderRect.Bottom - 2;

            r.Save();

            // draw upside down and on top (more space on bottom and visually separated from other sliders)
            if (textLetter == "A")
            {
                r.Scale(1, -1);
                cursorY = -sliderRect.Top - 2;
            }

            r.Translate(cursorX, cursorY);
            
            // draw little triangle handle
            paint.IsAntialias = true;

            paint.Color = SKColors.White;
            r.DrawPath(triangleHandle, paint);

            r.Scale(0.8f, 0.8f);
            r.Translate(0, 1.6f);

            paint.Color = SKColors.Black;
            r.DrawPath(triangleHandle, paint);

            paint.IsAntialias = false;
            
            r.Restore();
        }

        private readonly SKShader[] cacheShaders = new SKShader[4];

        // NOTE: RGB and HSV do not depend on alpha here, which is usually the case of some paint softwares
        // I just visually prefer this way, to implement it, it would require composed shader just like Alpha
        private SKShader GetShaderOf(string textLetter, in SKRect rect)
        {
            var p1 = new SKPoint(rect.Left, rect.Top);
            var p2 = new SKPoint(rect.Right, rect.Top);

            return textLetter switch
            {
                // return cached shader or create new
                // (set shader to null to force create new)

                "R" => cacheShaders[0] ??= SKShader.CreateLinearGradient(
                    p1, p2,
                    [
                        new(0, this.AmountG, this.AmountB),
                        new(255, this.AmountG, this.AmountB)
                    ],
                    SKShaderTileMode.Clamp
                ),

                "G" => cacheShaders[1] ??= SKShader.CreateLinearGradient(
                    p1, p2,
                    [
                        new(this.AmountR, 0, this.AmountB),
                        new(this.AmountR, 255, this.AmountB)
                    ],
                    SKShaderTileMode.Clamp
                ),

                "B" => cacheShaders[2] ??= SKShader.CreateLinearGradient(
                    p1, p2,
                    [
                        new(this.AmountR, this.AmountG, 0),
                        new(this.AmountR, this.AmountG, 255)
                    ],
                    SKShaderTileMode.Clamp
                ),

                // never changes (but still update it on windows move)
                "H" => cacheShaders[0] ??= SKShader.CreateLinearGradient(
                    p1, p2,
                    RGBColorWheel.wheelColors, null,
                    SKShaderTileMode.Clamp
                ),

                "S" => cacheShaders[1] ??= SKShader.CreateLinearGradient(
                    p1, p2,
                    [
                        // clamp to avoid crashes (e.g: moving saturation slider and then value slider)
                        SKColor.FromHsv(Math.Clamp(this.AmountH, 0, 360), 0, Math.Clamp(this.AmountV, 0, 100)),
                        SKColor.FromHsv(Math.Clamp(this.AmountH, 0, 360), 100, Math.Clamp(this.AmountV, 0, 100))
                    ],
                    SKShaderTileMode.Clamp
                ),

                "V" => cacheShaders[2] ??= SKShader.CreateLinearGradient(
                    p1, p2,
                    [
                        // clamp to avoid crashes (e.g: moving saturation slider and then value slider)
                        SKColor.FromHsv(Math.Clamp(this.AmountH, 0, 360), Math.Clamp(this.AmountS, 0, 100), 0), // always black
                        SKColor.FromHsv(Math.Clamp(this.AmountH, 0, 360), Math.Clamp(this.AmountS, 0, 100), 100)
                    ],
                    SKShaderTileMode.Clamp
                ),

                // composed shader (draw transparency shader and then gradient)
                "A" => cacheShaders[3] ??= SKShader.CreateCompose(

                    // slight offset (make it relative to rect so it doesn't move when window moves)
                    transparencyShader.WithLocalMatrix(SKMatrix.CreateTranslation(rect.Left, rect.Top)),
                    SKShader.CreateLinearGradient(
                        p1, p2,
                        [
                            new SKColor(this.AmountR, this.AmountG, this.AmountB, 0), // current color with alpha 0
                            new SKColor(this.AmountR, this.AmountG, this.AmountB, 255) // current color with alpha 255
                        ],
                        SKShaderTileMode.Clamp
                    ),
                    SKBlendMode.SrcOver // draw gradient on top
                ),
                
                _ => null! // throw new Exception("Unknown textLetter")
            };
        }

        private void UpdateShaders()
        {
            // set all to null (basically invalidate so it gets recalculated)
            foreach (ref var shader in cacheShaders.AsSpan())
            {
                shader?.Dispose();
                shader = null;
            }
        }

        private bool updatingFromSliders = false; // like in RGBColorWheel
        private bool updatingFromHex = false; // prevent infinite recursion
        public bool UpdatingValueOrAlpha => draggingSliders[3] /*alpha*/ || (!RGBMode && draggingSliders[2] /*value*/); // accessed from RGBColorWheel

        private void UpdateFromColor(SKColor color)
        {
            if (!updatingFromSliders)
            {
                this.AmountR = color.Red;
                this.AmountG = color.Green;
                this.AmountB = color.Blue;
                this.AmountA = color.Alpha;

                // can't out params directly because of the get; set;
                color.ToHsv(out float h, out float s, out float v);
                // round to 1 decimal place to ensure consistency and avoid flickering values
                (AmountH, AmountS, AmountV) = (MathF.Round(h, 2), MathF.Round(s, 2), MathF.Round(v, 2));
            }
            else
            {
                // When dragging sliders, update RGB amounts to keep them synchronized
                // but skip HSV conversion to prevent other sliders from moving
                this.AmountR = color.Red;
                this.AmountG = color.Green;
                this.AmountB = color.Blue;
                this.AmountA = color.Alpha;
            }

            UpdateShaders();
            if (!updatingFromHex)
                hexInputUI.UpdateHex(HexCode);
            UpdateSliderTextInputs();
        }

        private void UpdateSliderTextInputs() // (from amounts)
        {
            if (RGBMode)
            {
                sliderTextInputs[0].SetText(AmountR.ToString());
                sliderTextInputs[1].SetText(AmountG.ToString());
                sliderTextInputs[2].SetText(AmountB.ToString());
            }
            else
            {
                sliderTextInputs[0].SetText(AmountH.ToString("0"));
                sliderTextInputs[1].SetText(AmountS.ToString("0"));
                sliderTextInputs[2].SetText(AmountV.ToString("0"));
            }

            sliderTextInputs[3].SetText(AmountA.ToString()); // alpha
        }

        private static readonly Dictionary<string, float> TextLettersMaxValue = new()
        {
            ["R"] = 255, // byte 0-255
            ["G"] = 255, // byte 0-255
            ["B"] = 255, // byte 0-255
            ["H"] = 360f, // float 0-360
            ["S"] = 100f, // float 0-100
            ["V"] = 100f, // float 0-100
            ["A"] = 255, // byte 0-255
        };

        private static float GetPercentOf(float value, string textLetter)
            => value / TextLettersMaxValue[textLetter]; // (unsafe)
        
        // reverse method
        private static float GetValueFromMouse(SKRect sliderRect, SKPoint mousePos, string textLetter)
        {
            float percent = (mousePos.X - sliderRect.Left) / sliderRect.Width;
            percent = Math.Clamp(percent, 0, 1);
            
            return percent * TextLettersMaxValue[textLetter]; // (unsafe)
        }

        private void UpdateColorFromAmounts() // called each slider change
        {
            var newColor = RGBMode
                // build from RGBA amounts
                ? new SKColor(AmountR, AmountG, AmountB, AmountA)
                // build from HSVA amounts
                // clamp to avoid crashes (e.g: moving saturation slider and then value slider)
                : SKColor.FromHsv(Math.Clamp(AmountH, 0, 360), Math.Clamp(AmountS, 0, 100), Math.Clamp(AmountV, 0, 100), AmountA);

                // note that AmountH can become 0 internally even though it was 360 (because formally 0 == 360)

            updatingFromSliders = true;

            if (parent.editingPrimaryColor)
                toolManager.SetPrimaryColor(newColor);
            else
                toolManager.SetSecondaryColor(newColor);
            
            updatingFromSliders = false;
        }

        public void UpdateAmountsFromHex(string hex)
        {
            if (updatingFromHex)
                return;

            if (!uint.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out uint rgb))
                return;

            AmountR = (byte)(rgb >> 16);
            AmountG = (byte)(rgb >> 8);
            AmountB = (byte)(rgb & 0xFF);

            var newColor = new SKColor(AmountR, AmountG, AmountB, AmountA);
            newColor.ToHsv(out float h, out float s, out float v);

            // round to 2 decimal places for consistency
            AmountH = MathF.Round(h, 2);
            AmountS = MathF.Round(s, 2);
            AmountV = MathF.Round(v, 2);

            UpdateShaders();
            hexInputUI.UpdateHex(HexCode);
            UpdateSliderTextInputs();

            updatingFromSliders = true;
            updatingFromHex = true;

            if (parent.editingPrimaryColor)
                toolManager.SetPrimaryColor(newColor);
            else
                toolManager.SetSecondaryColor(newColor);
            
            updatingFromSliders = false;
            updatingFromHex = false;
        }

        private static readonly SKColor inputBoxColor = new(41, 41, 41);
        private static readonly SKColor shadowColor = new(65, 65, 65); // used for input box and sliders

        private class HexInput : UIComponent // inside AllSliders!
        {
            private SKRect wholeRect, textHitbox;
            private readonly UITextInput textInput = new(fontSize: 14f, initText: "000000", maxTextLength: 6)
            {
                AdjustKeyChar = char.ToUpperInvariant, // a-f letters become uppercase automatically

                // only allow hexadecimal characters (0-9, A-F only caps)
                KeyTypeCondition = (c) => (c >= '0' && c <= '9') || (c >= 'A' && c <= 'F'),
            };

            public HexInput(AllSliders parent)
            {
                // setup events of UITextInput that require internal methods:

                // invoked when focus is lost from Enter, Esc or click outside textHitbox
                textInput.OnFocusLostAction = () =>
                {
                    // e.g: "FF" was typed, then Enter is pressed -> "FF0000"
                    if (textInput.CurrText.Length < 6)
                        textInput.SetText(textInput.CurrText.PadRight(6, '0'), loseFocus: false);
                    
                    // update color
                    parent.UpdateAmountsFromHex(textInput.CurrText);
                };

                textInput.OnKeyCharAction = () =>
                {
                    if (textInput.CurrText.Length != 6) return;

                    // update color
                    parent.UpdateAmountsFromHex(textInput.CurrText);
                };
            }

            public void UpdateHex(string hex)
                => textInput.SetText(hex);

            public override void Compute(SKRect win) {}

            public void Update(SKRect thing) // basically Compute()
            {
                wholeRect = textHitbox = thing;
                textHitbox.Left += 12; // x offset of text (so it's on the right of the unselectable "#" character)

                textInput.UpdateContainer(textHitbox);
            }

            public override void Draw(SKCanvas r, double deltaTime, SKPaint paint)
            {
                // draw rect
                paint.IsAntialias = true;

                // shadow of input box
                paint.Color = AllSliders.shadowColor;
                wholeRect.DrawShadowRect(r, paint, 0, 0, 2f);

                // input box
                paint.Color = AllSliders.inputBoxColor;
                r.DrawRect(wholeRect, paint);

                paint.IsAntialias = false;

                // draw "HEX" text on the left
                UIManager.MainTextFont.Size = 14f;
                r.DrawText("HEX", wholeRect.Left - 36, wholeRect.MidY + 5, SKTextAlign.Left, UIManager.MainTextFont, UIManager.MainTextPaint);

                // draw unselectable "#" before text input
                var y = wholeRect.MidY - (UIManager.MainTextFont.Metrics.Ascent + UIManager.MainTextFont.Metrics.Descent) / 2; // centered
                r.DrawText("#", wholeRect.Left + 5, y, SKTextAlign.Left, UIManager.MainTextFont, UIManager.MainTextPaint);

                // draw text on rect
                textInput.Draw(r, deltaTime, paint);
            }

            #region All events

            public override bool OnMouseDown(bool leftDown, bool rightDown, SKPoint mousePos)
            {
                if (textInput.OnMouseDown(leftDown, rightDown, mousePos)) return true;

                return false;
            }

            public override bool OnMouseUp(bool leftDown, bool rightDown, SKPoint mousePos)
            {
                if (textInput.OnMouseUp(leftDown, rightDown, mousePos)) return true;
                return false;
            }

            public override bool OnMouseMove(bool leftDown, bool rightDown, SKPoint lastMousePos, SKPoint mousePos)
            {
                if (textInput.OnMouseMove(leftDown, rightDown, lastMousePos, mousePos)) return true;
                
                return false;
            }

            public override bool OnKeyDown(IKeyboard keyboard, Key key, int scancode, Modifier modifiers)
            {
                if (textInput.OnKeyDown(keyboard, key, scancode, modifiers)) return true;
                return false;
            }

            public override bool OnKeyUp(IKeyboard keyboard, Key key, int scancode, Modifier modifiers)
            {
                if (textInput.OnKeyUp(keyboard, key, scancode, modifiers)) return true;
                return false;
            }

            public override void OnKeyChar(IKeyboard keyboard, char c)
            {
                textInput.OnKeyChar(keyboard, c);
            }

            public override void OnFocusLost()
            {
                textInput.OnFocusLost();
            }

            #endregion
        }

        #region All events (for sliders and input boxes)

        private readonly bool[] draggingSliders = new bool[4];

        public override bool OnMouseDown(bool leftDown, bool rightDown, SKPoint mousePos)
        {
            sliderTextInputFocused = null;

            if (leftDown && ((switchRGBRect.Contains(mousePos) && !RGBMode) || (switchHSVRect.Contains(mousePos) && RGBMode)))
            {
                RGBMode = !RGBMode;

                // e.g: update maximum values input boxes can have (before with Hue: 360, now with Red: 255)
                var s1 = sliderTextInputs[0];
                var s2 = sliderTextInputs[1];
                var s3 = sliderTextInputs[2];
                var s4 = sliderTextInputs[3];

                UpdateSliderTextInputsEvents(s1, s2, s3, s4);

                // update value of input boxes
                UpdateSliderTextInputs();
                
                // update sliders shaders
                UpdateShaders();

                return true;
            }

            if (hexInputUI.OnMouseDown(leftDown, rightDown, mousePos)) return true;

            // do OnMouseDown for all and set new focused slider
            // only return AFTER, because the code inside s.OnMouseDown calls OnFocusLost if needed
            // so OnMouseDown of every sliderTextInput must be called first
            bool focused = false;
            foreach (var s in sliderTextInputs)
            {
                if (s.OnMouseDown(leftDown, rightDown, mousePos))
                {
                    sliderTextInputFocused = s;
                    focused = true;
                }
            }

            if (focused) return true;

            if (leftDown)
            {
                for (int i = 0; i < slidersRect.Length; i++)
                {
                    if (slidersRect[i].Contains(mousePos))
                    {
                        draggingSliders[i] = true;
                        break;
                    }
                }

                // equivalent to old:
                // if (slider1Rect.Contains(mousePos)) draggingSlider1 = true;
                // else if (slider2Rect.Contains(mousePos)) draggingSlider2 = true;
                // else if (slider3Rect.Contains(mousePos)) draggingSlider3 = true;
                // else if (slider4Rect.Contains(mousePos)) draggingSlider4 = true;
            }

            // old: if (draggingSlider1 || draggingSlider2 || draggingSlider3 || draggingSlider4)
            if (draggingSliders.Contains(true))
            {
                // so that the triangle handle snaps to mouse position right away
                this.OnMouseMove(leftDown, rightDown, mousePos, mousePos);
                return true;
            }

            return false;
        }

        public override bool OnMouseUp(bool leftDown, bool rightDown, SKPoint mousePos)
        {
            if (hexInputUI.OnMouseUp(leftDown, rightDown, mousePos)) return true;

            if (sliderTextInputFocused != null)
                if (sliderTextInputFocused.OnMouseUp(leftDown, rightDown, mousePos)) return true;

            for (int i = 0; i < slidersRect.Length; i++)
                draggingSliders[i] = false;

            return false;
        }

        public override bool OnMouseMove(bool leftDown, bool rightDown, SKPoint lastMousePos, SKPoint mousePos)
        {
            if (hexInputUI.OnMouseMove(leftDown, rightDown, lastMousePos, mousePos)) return true;
            
            if (sliderTextInputFocused != null)
                if (sliderTextInputFocused.OnMouseMove(leftDown, rightDown, lastMousePos, mousePos)) return true;

            if (leftDown)
            {
                if (draggingSliders[0])
                {
                    var val = GetValueFromMouse(slidersRect[0], mousePos, RGBMode ? "R" : "H");

                    if (RGBMode)
                        AmountR = (byte)val;
                    else
                        AmountH = val;
                }
                else if (draggingSliders[1])
                {
                    var val = GetValueFromMouse(slidersRect[1], mousePos, RGBMode ? "G" : "S");

                    if (RGBMode)
                        AmountG = (byte)val;
                    else
                        AmountS = val;
                }
                else if (draggingSliders[2])
                {
                    var val = GetValueFromMouse(slidersRect[2], mousePos, RGBMode ? "B" : "V");

                    if (RGBMode)
                        AmountB = (byte)val;
                    else
                        AmountV = val;
                }
                else if (draggingSliders[3])
                {
                    // alpha is always the same, so set directly
                    AmountA = (byte)GetValueFromMouse(slidersRect[3], mousePos, "A");
                }
            }

            // old: if (draggingSlider1 || draggingSlider2 || draggingSlider3 || draggingSlider4)
            if (draggingSliders.Contains(true))
            {
                UpdateShaders();
                UpdateColorFromAmounts(); // edit toolManager.PrimaryColor color

                // NOTE: no need to update wheel if dragged alpha or value
                return true;
            }
            
            return false;
        }

        public override bool OnKeyDown(IKeyboard keyboard, Key key, int scancode, Modifier modifiers)
        {
            if (hexInputUI.OnKeyDown(keyboard, key, scancode, modifiers)) return true;

            if (sliderTextInputFocused != null)
                if (sliderTextInputFocused.OnKeyDown(keyboard, key, scancode, modifiers)) return true;

            return false;
        }

        public override bool OnKeyUp(IKeyboard keyboard, Key key, int scancode, Modifier modifiers)
        {
            if (hexInputUI.OnKeyUp(keyboard, key, scancode, modifiers)) return true;

            if (sliderTextInputFocused != null)
                if (sliderTextInputFocused.OnKeyUp(keyboard, key, scancode, modifiers)) return true;
            
            return false;
        }

        public override void OnKeyChar(IKeyboard keyboard, char c)
        {
            hexInputUI.OnKeyChar(keyboard, c);

            sliderTextInputFocused?.OnKeyChar(keyboard, c);
        }

        public override void OnFocusLost()
        {
            hexInputUI.OnFocusLost();

            sliderTextInputFocused?.OnFocusLost();
            sliderTextInputFocused = null;
        }

        #endregion
    }

    #endregion

    public override bool OnMouseDown(bool leftDown, bool rightDown, SKPoint mousePos)
    {
        if (base.OnMouseDown(leftDown, rightDown, mousePos)) return true;

        // manage ui components event
        foreach (var ui in UIComponents)
            if (ui.OnMouseDown(leftDown, rightDown, mousePos)) return true;
        
        if (base.ContentRect.Contains(mousePos))
            return true; // always return true if click is inside UI
        
        return false;
    }

    public override bool OnMouseUp(bool leftDown, bool rightDown, SKPoint mousePos)
    {
        if (base.OnMouseUp(leftDown, rightDown, mousePos)) return true;

        // manage ui components event
        foreach (var ui in UIComponents)
            if (ui.OnMouseUp(leftDown, rightDown, mousePos)) return true;

        return false;
    }

    public override bool OnMouseMove(bool leftDown, bool rightDown, SKPoint lastMousePos, SKPoint mousePos)
    {
        if (base.OnMouseMove(leftDown, rightDown, lastMousePos, mousePos)) return true;

        // manage ui components event
        foreach (var ui in UIComponents)
            if (ui.OnMouseMove(leftDown, rightDown, lastMousePos, mousePos)) return true;
        
        // always return if mouse is inside window (so windows behind aren't processed)
        if (base.HeaderRect.Contains(mousePos) || base.ContentRect.Contains(mousePos))
            return true;

        return false;
    }

    public override bool OnKeyDown(IKeyboard keyboard, Key key, int scancode, Modifier modifiers)
    {
        // FloatingWindow doesn't use OnKeyDown at all as for now
        // if (base.OnKeyDown(key, scancode)) return true;

        // manage ui components event
        foreach (var ui in UIComponents)
            if (ui.OnKeyDown(keyboard, key, scancode, modifiers)) return true;

        return false;
    }

    public override bool OnKeyUp(IKeyboard keyboard, Key key, int scancode, Modifier modifiers)
    {
        // FloatingWindow doesn't use OnKeyUp at all as for now
        // if (base.OnKeyUp(key, scancode)) return true;

        // manage ui components event
        foreach (var ui in UIComponents)
            if (ui.OnKeyUp(keyboard, key, scancode, modifiers)) return true;

        return false;
    }

    public override void OnKeyChar(IKeyboard keyboard, char c)
    {
        // FloatingWindow doesn't use OnKeyChar at all as for now
        // base.OnKeyChar(c);

        foreach (var ui in UIComponents)
            ui.OnKeyChar(keyboard, c);
    }

    public override void OnFocusLost()
    {
        base.OnFocusLost();

        // manage ui components event
        foreach (var ui in UIComponents)
            ui.OnFocusLost();
    }
}