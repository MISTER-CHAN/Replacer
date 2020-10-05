using Android.App;
using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Support.V7.App;
using Android.Util;
using Android.Views;
using Android.Widget;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using static Android.Graphics.PorterDuff;

namespace Replacer
{

    public class TabHostEx : TabHost
    {
        public TabHostEx(Context context) : base(context)
        {
        }

        public TabHostEx(Context context, IAttributeSet attrs) : base(context, attrs)
        {
        }
        
        public override void OnTouchModeChanged(bool isInTouchMode)
        {
            // base.OnTouchModeChanged(isInTouchMode);
        }
    }

    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme", MainLauncher = true)]
    public class MainActivity : AppCompatActivity
    {
        private Bitmap bitmap;
        private Button bInitCharmap, bPasteOld, bReplace, bSelectChar;
        byte descriptionSource = 1;
        private Canvas canvas;
        private CheckBox cbSingleline, cbIgnoreCase, cbMultiline, cbMultiold, cbSplit, cbMultinew;
        private ClipboardManager clipboard;
        private EditText etChar, etCombiningChar, etGotoChar, etNumber, etOld, etRegex, etRegexNew, etString, etNew;
        private ImageView ivCharmap;
        private int first = 0, lastSymbols = 0, selLength = 1, selStart = 0;
        private LinearLayout llCharmap, llCode, llController;
        private readonly Paint paint = new Paint() {
            TextSize = 36
        };
        private SeekBar sbSymbols;
        private Spinner sCharmap;
        private string progress;
        private readonly string[] descriptions = new string[0x20000];
        private TabHostEx tabHost;
        private TextView tvChar, tvCharUnicode, tvCharDescription, tvNextChar, tvPrevChar, tvCharPreview, tvStatus;
        private ToggleButton tbChars, tbSelect;
        private Thread loadDescription;

        public struct Data
        {
            public int Message;
        }

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            // Set our view from the "main" layout resource
            SetContentView(Resource.Layout.activity_main);

            ServicePointManager.ServerCertificateValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;

            tabHost = FindViewById<TabHostEx>(Resource.Id.tab_host);
            tabHost.Setup();
            LayoutInflater inflater = LayoutInflater.From(this);
            inflater.Inflate(Resource.Layout.replace, tabHost.TabContentView);
            inflater.Inflate(Resource.Layout.regex, tabHost.TabContentView);
            inflater.Inflate(Resource.Layout.move_symbols, tabHost.TabContentView);
            inflater.Inflate(Resource.Layout.combine, tabHost.TabContentView);
            inflater.Inflate(Resource.Layout.@string, tabHost.TabContentView);
            inflater.Inflate(Resource.Layout.style, tabHost.TabContentView);
            inflater.Inflate(Resource.Layout.encoder, tabHost.TabContentView);
            inflater.Inflate(Resource.Layout.charmap, tabHost.TabContentView);
            tabHost.AddTab(tabHost.NewTabSpec("").SetIndicator("替換").SetContent(Resource.Id.ll_replace));
            tabHost.AddTab(tabHost.NewTabSpec("").SetIndicator("正則表達式").SetContent(Resource.Id.ll_regex));
            tabHost.AddTab(tabHost.NewTabSpec("symbols").SetIndicator("符號移動").SetContent(Resource.Id.ll_arrows));
            tabHost.AddTab(tabHost.NewTabSpec("").SetIndicator("組合").SetContent(Resource.Id.ll_combine));
            tabHost.AddTab(tabHost.NewTabSpec("").SetIndicator("重複").SetContent(Resource.Id.ll_string));
            tabHost.AddTab(tabHost.NewTabSpec("").SetIndicator("樣式").SetContent(Resource.Id.ll_style));
            tabHost.AddTab(tabHost.NewTabSpec("").SetIndicator("編碼").SetContent(Resource.Id.ll_encoder));
            tabHost.AddTab(tabHost.NewTabSpec("charmap").SetIndicator("字符映射表").SetContent(Resource.Id.ll_charmap_main));

            bInitCharmap = FindViewById<Button>(Resource.Id.b_init_charmap);
            bPasteOld = FindViewById<Button>(Resource.Id.b_paste_old);
            bReplace = FindViewById<Button>(Resource.Id.b_replace);
            bSelectChar = FindViewById<Button>(Resource.Id.b_select_char);
            cbIgnoreCase = FindViewById<CheckBox>(Resource.Id.cb_ignore_case);
            cbMultiline = FindViewById<CheckBox>(Resource.Id.cb_multiline);
            cbMultinew = FindViewById<CheckBox>(Resource.Id.cb_multinew);
            cbMultiold = FindViewById<CheckBox>(Resource.Id.cb_multiold);
            cbSingleline = FindViewById<CheckBox>(Resource.Id.cb_singleline);
            cbSplit = FindViewById<CheckBox>(Resource.Id.cb_split);
            clipboard = (ClipboardManager)GetSystemService(ClipboardService);
            ivCharmap = FindViewById<ImageView>(Resource.Id.iv_charmap);
            etChar = FindViewById<EditText>(Resource.Id.et_char);
            etCombiningChar = FindViewById<EditText>(Resource.Id.et_combining_char);
            etGotoChar = FindViewById<EditText>(Resource.Id.et_goto_char);
            etNew = FindViewById<EditText>(Resource.Id.et_new);
            etNumber = FindViewById<EditText>(Resource.Id.et_number);
            etOld = FindViewById<EditText>(Resource.Id.et_old);
            etRegex = FindViewById<EditText>(Resource.Id.et_regex);
            etRegexNew = FindViewById<EditText>(Resource.Id.et_regex_new);
            etString = FindViewById<EditText>(Resource.Id.et_string);
            llCharmap = FindViewById<LinearLayout>(Resource.Id.ll_charmap);
            llCode = FindViewById<LinearLayout>(Resource.Id.ll_code);
            llController = FindViewById<LinearLayout>(Resource.Id.ll_controller);
            sbSymbols = FindViewById<SeekBar>(Resource.Id.sbSymbols);
            sCharmap = FindViewById<Spinner>(Resource.Id.s_charmap);
            tbChars = FindViewById<ToggleButton>(Resource.Id.tb_chars);
            tbSelect = FindViewById<ToggleButton>(Resource.Id.tb_select);
            tvChar = FindViewById<TextView>(Resource.Id.tv_char);
            tvCharUnicode = FindViewById<TextView>(Resource.Id.tv_char_unicode);
            tvCharDescription = FindViewById<TextView>(Resource.Id.tv_char_description);
            tvCharPreview = FindViewById<TextView>(Resource.Id.tv_char_preview);
            tvPrevChar = FindViewById<TextView>(Resource.Id.tv_prev_char);
            tvNextChar = FindViewById<TextView>(Resource.Id.tv_next_char);
            tvStatus = FindViewById<TextView>(Resource.Id.tv_status);

            FindViewById<Button>(Resource.Id.b_bold).Click += BBold_Click;
            FindViewById<Button>(Resource.Id.b_charmap_next_page).Click += BCharmapNextPage_Click;
            FindViewById<Button>(Resource.Id.b_charmap_prev_page).Click += BCharmapPrevPage_Click;
            FindViewById<Button>(Resource.Id.b_combine).Click += BCombine_Click;
            FindViewById<Button>(Resource.Id.b_copy).Click += BCopy_Click;
            FindViewById<Button>(Resource.Id.b_copy_char).Click += BCopyChar_Click;
            FindViewById<Button>(Resource.Id.b_delete).Click += BDelete_Click;
            FindViewById<Button>(Resource.Id.b_doublestruck).Click += BDoublestruck_Click;
            FindViewById<Button>(Resource.Id.b_fraktur).Click += BFraktur_Click;
            FindViewById<Button>(Resource.Id.b_goto_char).Click += BGotoChar_Click;
            bInitCharmap.Click += BInitCharmap_Click;
            FindViewById<Button>(Resource.Id.b_italic).Click += BItalic_Click;
            FindViewById<Button>(Resource.Id.b_left).Click += BLeft_Click;
            FindViewById<Button>(Resource.Id.b_lower).Click += BLower_Click;
            FindViewById<Button>(Resource.Id.b_paste).Click += BPaste_Click;
            FindViewById<Button>(Resource.Id.b_paste_combining).Click += BPasteCombining_Click;
            FindViewById<Button>(Resource.Id.b_paste_left).Click += BPasteLeft_Click;
            FindViewById<Button>(Resource.Id.b_paste_right).Click += BPasteRight_Click;
            FindViewById<Button>(Resource.Id.b_paste_new).Click += BPasteNew_Click;
            bPasteOld.Click += BPasteOld_Click;
            bPasteOld.LongClick += BPasteOld_LongClick;
            FindViewById<Button>(Resource.Id.b_pick_combining).Click += BPickCombining_Click;
            bReplace.Click += BReplace_Click;
            FindViewById<Button>(Resource.Id.b_regex_replace).Click += BRegexReplace;
            FindViewById<Button>(Resource.Id.b_right).Click += BRight_Click;
            FindViewById<Button>(Resource.Id.b_roman_numeral).Click += BRomanNumeral;
            FindViewById<Button>(Resource.Id.b_sansserif_bold).Click += BSansserifBold_Click;
            FindViewById<Button>(Resource.Id.b_sansserif_italic).Click += BSansserifItalic_Click;
            FindViewById<Button>(Resource.Id.b_script).Click += BScript_Click;
            bSelectChar.Click += BSelectChar_Click;
            bSelectChar.LongClick += BSelectChar_LongClick;
            FindViewById<Button>(Resource.Id.b_small).Click += BSmall_Click;
            FindViewById<Button>(Resource.Id.b_strikethrough).Click += BStrikethrough_Click;
            FindViewById<Button>(Resource.Id.b_string).Click += BString_Click;
            FindViewById<Button>(Resource.Id.b_string_2).Click += BString2_Click;
            FindViewById<Button>(Resource.Id.b_string_3).Click += BString3_Click;
            FindViewById<Button>(Resource.Id.b_subscript).Click += BSubscript;
            FindViewById<Button>(Resource.Id.b_superscript).Click += BSuperscript;
            FindViewById<Button>(Resource.Id.b_turned).Click += BTurned_Click;
            FindViewById<Button>(Resource.Id.b_underline).Click += BUnderline_Click;
            FindViewById<Button>(Resource.Id.b_unicode_encode).Click += BUnicodeEncode_Click;
            FindViewById<Button>(Resource.Id.b_upper).Click += BUpper_Click;
            etChar.KeyPress += EtChar_KeyPress;
            etString.TextChanged += EtString_TextChanged;
            ivCharmap.Touch += IvCharmap_Touch;
            sbSymbols.ProgressChanged += SbSymbols_ProgressChanged;
            tabHost.TabChanged += TabHost_TabChanged;
            tbChars.CheckedChange += TbChars_CheckedChange;
            tbSelect.CheckedChange += TbSelect_CheckedChange;
            tvChar.Click += TvChars_Click;
            tvChar.LongClick += TvChar_LongClick;

            loadDescription = new Thread(LoadDescription);
            loadDescription.Start();
        }

        private void BBold_Click(object sender, EventArgs e)
        {
            SetStringSelection(Xlit(GetStringSelecion(),
                "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnoprstuvwxyzΑΒΓΔΕΖΗΘΙΚΛΜΝΞΟΠΡΣΤΥΦΧΨΩϜ∇αβγδεζηθικλμνξοπρςστυφχψω∂ϵϑϰϕϱϖ",
                "𝟎𝟏𝟐𝟑𝟒𝟓𝟔𝟕𝟖𝟗𝐀𝐁𝐂𝐃𝐄𝐅𝐆𝐇𝐈𝐉𝐊𝐋𝐌𝐍𝐎𝐏𝐐𝐑𝐒𝐓𝐔𝐕𝐖𝐗𝐘𝐙𝐚𝐛𝐜𝐝𝐞𝐟𝐠𝐡𝐢𝐣𝐤𝐥𝐦𝐧𝐨𝐩𝐪𝐫𝐬𝐭𝐮𝐯𝐰𝐱𝐲𝐳𝚨𝚩𝚪𝚫𝚬𝚭𝚮𝚯𝚰𝚱𝚲𝚳𝚴𝚵𝚶𝚷𝚸𝚹𝚺𝚻𝚼𝚽𝚾𝚿𝛀𝛁𝟊𝛂𝛃𝛄𝛅𝛆𝛇𝛈𝛉𝛊𝛋𝛌𝛍𝛎𝛏𝛐𝛑𝛒𝛓𝛔𝛕𝛖𝛗𝛘𝛙𝛚𝛛𝛜𝛝𝛞𝛟𝛠𝛡"));
        }

        private void BCharmapNextPage_Click(object sender, EventArgs e)
        {
            if (first < 0x10ff00)
            {
                first += 0x100;
                ShowCharsOnCharmap();
                if (first % 0x1000 == 0)
                {
                    SetCharmapSpinnerSelection();
                }
            }
        }

        private void BCharmapPrevPage_Click(object sender, EventArgs e)
        {
            if (first > 0)
            {
                first -= 0x100;
                ShowCharsOnCharmap();
                if (first % 0x1000 == 0xf00)
                {
                    SetCharmapSpinnerSelection();
                }
            }
        }

        private void BCombine_Click(object sender, EventArgs e)
        {
            etString.Text = Combine(etString.Text, etCombiningChar.Text);
        }

        private void BCopy_Click(object sender, EventArgs e)
        {
            clipboard.PrimaryClip = ClipData.NewPlainText("text", GetString());
        }

        private void BCopyChar_Click(object sender, EventArgs e)
        {
            clipboard.PrimaryClip = ClipData.NewPlainText("text", tvCharPreview.Text);
        }

        private void BDelete_Click(object sender, EventArgs e)
        {
            SetString("");
        }

        private void BDoublestruck_Click(object sender, EventArgs e)
        {
            SetStringSelection(Xlit(GetStringSelecion(),
                "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnoprstuvwxyz",
                "𝟘𝟙𝟚𝟛𝟜𝟝𝟞𝟟𝟠𝟡𝔸𝔹ℂ𝔻𝔼𝔽𝔾ℍ𝕀𝕁𝕂𝕃𝕄ℕ𝕆ℙℚℝ𝕊𝕋𝕌𝕍𝕎𝕏𝕐ℤ𝕒𝕓𝕔𝕕𝕖𝕗𝕘𝕙𝕚𝕛𝕜𝕝𝕞𝕟𝕠𝕡𝕢𝕣𝕤𝕥𝕦𝕧𝕨𝕩𝕪𝕫"));
        }

        private void BFraktur_Click(object sender, EventArgs e)
        {
            SetStringSelection(Xlit(GetStringSelecion(),
                "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnoprstuvwxyz",
                "𝔄𝔅ℭ𝔇𝔈𝔉𝔊ℋℑ𝔍𝔎𝔏𝔐𝔑𝔒𝔓𝔔ℜ𝔖𝔗𝔘𝔙𝔚𝔛𝔜ℨ𝔞𝔟𝔠𝔡𝔢𝔣𝔤𝔥𝔦𝔧𝔨𝔩𝔪𝔫𝔬𝔭𝔮𝔯𝔰𝔱𝔲𝔳𝔴𝔵𝔶𝔷"));
        }

        private void BGotoChar_Click(object sender, EventArgs e)
        {
            if (new Regex("^.[0-9A-Fa-f]+$").IsMatch(etGotoChar.Text))
            {
                first = (int)Math.Floor((double)Convert.ToInt32(etGotoChar.Text, 16) / 256) * 256;
            }
            else
            {
                string s = UnicodeEncode(etGotoChar.Text)[0];
                if (!s.Contains("|"))
                {
                    first = (int)Math.Floor((double)Convert.ToInt32(s, 16) / 256) * 256;
                }
            }
            ShowCharsOnCharmap();
            SetCharmapSpinnerSelection();
        }

        private void BInitCharmap_Click(object sender, EventArgs e)
        {
            bInitCharmap.Visibility = ViewStates.Gone;
            ivCharmap.Visibility = ViewStates.Visible;
            llController.Visibility = ViewStates.Visible;
            bitmap = Bitmap.CreateBitmap(llCharmap.Width, llCharmap.Height, Bitmap.Config.Argb8888);
            canvas = new Canvas(bitmap);
            canvas.Translate(0, llCharmap.Height / 64);
            ivCharmap.SetImageBitmap(bitmap);
            ShowCharsOnCharmap();
            sCharmap.Adapter = new ArrayAdapter<string>(this, Android.Resource.Layout.SimpleSpinnerDropDownItem, new string[]
            {
                        "0000 ~ 0FFF", "1000 ~ 1FFF", "2000 ~ 2FFF", "3000 ~ 3FFF", "4000 ~ 9FFF", "A000 ~ AFFF", "B000 ~ EFFF", "F000 ~ FFFF", "10000 ~ 10FFFF"
            });
            sCharmap.ItemSelected += SpinnerCharmap_ItemSelected;
        }

        private void BItalic_Click(object sender, EventArgs e)
        {
            SetStringSelection(Xlit(GetStringSelecion(),
                "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnoprstuvwxyzıΑΒΓΔΕΖΗΘΙΚΛΜΝΞΟΠΡΣΤΥΦΧΨΩϜ∇αβγδεζηθικλμνξοπρςστυφχψω∂ϵϑϰϕϱϖ",
                "𝐴𝐵𝐶𝐷𝐸𝐹𝐺𝐻𝐼𝐽𝐾𝐿𝑀𝑁𝑂𝑃𝑄𝑅𝑆𝑇𝑈𝑉𝑊𝑋𝑌𝑍𝑎𝑏𝑐𝑑𝑒𝑓𝑔ℎ𝑖𝑗𝑘𝑙𝑚𝑛𝑜𝑝𝑞𝑟𝑠𝑡𝑢𝑣𝑤𝑥𝑦𝑧𝚤𝛢𝛣𝛤𝛥𝛦𝛧𝛨𝛩𝛪𝛫𝛬𝛭𝛮𝛯𝛰𝛱𝛲𝛳𝛴𝛵𝛶𝛷𝛸𝛹𝛺𝛻𝛼𝛽𝛾𝛿𝜀𝜁𝜂𝜃𝜄𝜅𝜆𝜇𝜈𝜉𝜊𝜋𝜌𝜍𝜎𝜏𝜐𝜑𝜒𝜓𝜔𝜕𝜖𝜗𝜘𝜙𝜚𝜛"));
        }

        private void BLeft_Click(object sender, EventArgs e)
        {
            if (tbSelect.Checked)
            {
                if (selLength > 1)
                {
                    selLength--;
                    SelectChars();
                }
            }
            else
            {
                if (selStart > 0)
                {
                    selStart--;
                    SelectChars();
                }
            }
        }

        private void BLower_Click(object sender, EventArgs e)
        {
            SetStringSelection(GetStringSelecion().ToLower());
        }

        private void BPaste_Click(object sender, EventArgs e)
        {
            SetString(clipboard.PrimaryClip.GetItemAt(0).Text);
        }

        private void BPasteCombining_Click(object sender, EventArgs e)
        {
            etCombiningChar.Text = clipboard.PrimaryClip.GetItemAt(0).Text;
        }

        private void BPasteLeft_Click(object sender, EventArgs e)
        {
            etString.Text = etString.Text.Substring(0, selStart)
                + clipboard.PrimaryClip.GetItemAt(0).Text
                + etString.Text.Substring(selStart);
        }

        private void BPasteNew_Click(object sender, EventArgs e)
        {
            etNew.Text = clipboard.PrimaryClip.GetItemAt(0).Text;
        }

        private void BPasteOld_Click(object sender, EventArgs e)
        {
            etOld.Text = clipboard.PrimaryClip.GetItemAt(0).Text;
        }

        private void BPasteOld_LongClick(object sender, View.LongClickEventArgs e)
        {
            etOld.Text = CleanSymbols(clipboard.PrimaryClip.GetItemAt(0).Text);
        }

        private void BPasteRight_Click(object sender, EventArgs e)
        {
            etString.Text = etString.Text.Substring(0, selStart + selLength)
                + clipboard.PrimaryClip.GetItemAt(0).Text
                + etString.Text.Substring(selStart + selLength);
        }

        private void BPickCombining_Click(object sender, EventArgs e)
        {
            bool found = false;
            string combiningChar = "";
            for (int i = etString.Text.Length - 1; i >= 0; i--)
            {
                char c = etString.Text[i];
                if (IsCJKUI(c))
                {
                    if (found)
                    {
                        break;
                    }
                    else
                    {
                        continue;
                    }
                }
                else
                {
                    combiningChar = c + combiningChar;
                    found = true;
                }
            }
            etCombiningChar.Text = combiningChar;
        }

        private void BRegexReplace(object sender, EventArgs e)
        {
            etString.Text = new Regex(etRegex.Text, (cbIgnoreCase.Checked ? RegexOptions.IgnoreCase : 0) | (cbMultiline.Checked ? RegexOptions.Multiline : 0) | (cbSingleline.Checked ? RegexOptions.Singleline : 0)).Replace(etString.Text, etRegexNew.Text);
        }

        private void BReplace_Click(object sender, EventArgs e)
        {
            char separator = '\0';
            string replacedString = "";
            string[] olds = new string[1], news = new string[1];
            if (cbMultiold.Checked)
            {
                char s = '\0';
                foreach (char c in etOld.Text)
                {
                    if (!IsCJKUI(c))
                    {
                        s = c;
                        break;
                    }
                }
                olds = etOld.Text.Split(s);
            }
            else
            {
                if (etOld.Text != "")
                {
                    olds[0] = etOld.Text;
                }
                else
                {
                    olds[0] = CleanSymbols(etString.Text);
                }
            }
            if (cbMultinew.Checked)
            {
                char s = '\0';
                foreach (char c in etNew.Text)
                {
                    if (!IsCJKUI(c))
                    {
                        s = c;
                        break;
                    }
                }
                news = etNew.Text.Split(s);
                separator = s;
            }
            else
            {
                news[0] = etNew.Text;
            }
            foreach (string w in news)
            {
                string r = etString.Text;
                foreach (string f in olds)
                {
                    if (cbSplit.Checked && !etString.Text.Contains(f))
                    {
                        string letter = "";
                        List<string> symbols = new List<string>
                        {
                            ""
                        };
                        for (int i = 0; i < r.Length; i++)
                        {
                            char Char = r[i];
                            if (f.Contains(Char))
                            {
                                letter += Char;
                                symbols.Add("");
                            }
                            else
                            {
                                symbols[^1] += Char;
                            }
                        }
                        letter = letter.Replace(f, w);
                        r = "";
                        for (int i = 0; i < letter.Length; i++)
                        {
                            r += symbols[i] + letter[i];
                        }
                        if (symbols.Count > letter.Length)
                            r += symbols[^1];
                    }
                    else
                    {
                        r = r.Replace(f, w);
                    }
                }
                replacedString += r + separator;
            }
            etString.Text = replacedString[0..^1];
        }

        private void BRight_Click(object sender, EventArgs e)
        {
            if (tbSelect.Checked)
            {
                if (selLength < etString.Text.Length - selStart)
                {
                    selLength++;
                    SelectChars();
                }
            }
            else
            {
                if (selStart < etString.Text.Length - 1)
                {
                    selStart++;
                    SelectChars();
                }
            }
        }

        private void BRomanNumeral(object sender, EventArgs e)
        {
            const int ARABIC = 0, LATIN = 1, ROMAN = 2;
            string[] regexs = {
                "^\\d+$", "^[IVXLCDMivxlcdm̅]+$", "^[ⅠⅤⅩⅬⅭⅮⅯⅰⅴⅹⅼⅽⅾⅿ̅]+$"
            };
            byte prevCharType = 4;
            List<string>[] words = {
                new List<string>(), new List<string>(), new List<string>(), new List<string>()
            };
            foreach (char c in etString.Text)
            {
                byte currentCharType = 3;
                for (byte t = 0; t < regexs.Length; t++)
                {
                    if (new Regex(regexs[t]).IsMatch(c.ToString()))
                    {
                        currentCharType = t;
                        break;
                    }
                }
                if (currentCharType != prevCharType)
                {
                    for (int t = 0; t < words.Length; t++)
                    {
                        words[t].Add("");
                    }
                }
                words[currentCharType][^1] += c;
                prevCharType = currentCharType;
            }

            string result = "";
            for (int w = 0; w < words[0].Count; w++)
            {
                string numeral = "";
                for (int t = 0; t < words.Length; t++)
                {
                    numeral += words[t][w];
                }
                if (new Regex(regexs[ARABIC]).IsMatch(numeral))
                {
                    string[,] romanDigits = {
                        {
                            "", "X", "XX", "XXX", "XL", "L", "LX", "LXX", "LXXX", "XC"
                        },
                        {
                            "", "C", "CC", "CCC", "CD", "D", "DC", "DCC", "DCCC", "CM"
                        },
                        {
                            "", "M", "MM", "MMM", "MV̅", "V̅", "V̅M", "V̅MM", "V̅MMM", "MX̅"
                        },
                        {
                            "", "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX"
                        }
                    };
                    string arabicNumeral = numeral, romanNumeral = "";
                    for (int i = 0; i < arabicNumeral.Length; i++)
                    {
                        int arabicDigit = int.Parse(arabicNumeral.Substring(arabicNumeral.Length - 1 - i, 1));
                        if (i == 0)
                        {
                            romanNumeral = romanDigits[3, arabicDigit];
                        }
                        else
                        {
                            romanNumeral = Combine(romanDigits[(i - 1) % 3, arabicDigit], new string('̅', (int)Math.Ceiling((double)i / 3) - 1)) + romanNumeral;
                        }
                    }
                    result += romanNumeral;
                }
                else if (new Regex(regexs[LATIN]).IsMatch(numeral))
                {
                    result += Xlit(numeral, "IVXLCDMivxlcdm", "ⅠⅤⅩⅬⅭⅮⅯⅰⅴⅹⅼⅽⅾⅿ");
                }
                else if (new Regex(regexs[ROMAN]).IsMatch(numeral))
                {
                    result += numeral.Replace("ⅩⅠⅠ", "Ⅻ").Replace("ⅹⅰⅰ", "ⅻ")
                        .Replace("ⅩⅠ", "Ⅺ").Replace("ⅹⅰ", "ⅺ")
                        .Replace("ⅠⅩ", "Ⅸ").Replace("ⅰⅹ", "ⅸ")
                        .Replace("ⅤⅠⅠⅠ", "Ⅷ").Replace("ⅴⅰⅰⅰ", "ⅷ")
                        .Replace("ⅤⅠⅠ", "Ⅶ").Replace("ⅴⅰⅰ", "ⅶ")
                        .Replace("ⅤⅠ", "Ⅵ").Replace("ⅴⅰ", "ⅵ")
                        .Replace("ⅠⅤ", "Ⅳ").Replace("ⅰⅴ", "ⅳ")
                        .Replace("ⅠⅠⅠ", "Ⅲ").Replace("ⅰⅰⅰ", "ⅲ")
                        .Replace("ⅠⅠ", "Ⅱ").Replace("ⅰⅰ", "ⅱ");
                }
                else
                {
                    result += numeral;
                }
            }
            etString.Text = result;
        }

        private void BSansserifBold_Click(object sender, EventArgs e)
        {
            SetStringSelection(Xlit(GetStringSelecion(),
                "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnoprstuvwxyzΑΒΓΔΕΖΗΘΙΚΛΜΝΞΟΠΡΣΤΥΦΧΨΩϜ∇αβγδεζηθικλμνξοπρςστυφχψω∂ϵϑϰϕϱϖ",
                "𝟬𝟭𝟮𝟯𝟰𝟱𝟲𝟳𝟴𝟵𝗔𝗕𝗖𝗗𝗘𝗙𝗚𝗛𝗜𝗝𝗞𝗟𝗠𝗡𝗢𝗣𝗤𝗥𝗦𝗧𝗨𝗩𝗪𝗫𝗬𝗭𝗮𝗯𝗰𝗱𝗲𝗳𝗴𝗵𝗶𝗷𝗸𝗹𝗺𝗻𝗼𝗽𝗾𝗿𝘀𝘁𝘂𝘃𝘄𝘅𝘆𝘇𝝖𝝗𝝘𝝙𝝚𝝛𝝜𝝝𝝞𝝟𝝠𝝡𝝢𝝣𝝤𝝥𝝦𝝧𝝨𝝩𝝪𝝫𝝬𝝭𝝮𝝯𝝰𝝱𝝲𝝳𝝴𝝵𝝶𝝷𝝸𝝹𝝺𝝻𝝼𝝽𝝾𝝿𝞀𝞁𝞂𝞃𝞄𝞅𝞆𝞇𝞈𝞉𝞊𝞋𝞌𝞍𝞎𝞏"));
        }

        private void BSansserifItalic_Click(object sender, EventArgs e)
        {
            SetStringSelection(Xlit(GetStringSelecion(),
                "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnoprstuvwxyzΑΒΓΔΕΖΗΘΙΚΛΜΝΞΟΠΡΣΤΥΦΧΨΩϜ∇αβγδεζηθικλμνξοπρςστυφχψω∂ϵϑϰϕϱϖ",
                "𝘈𝘉𝘊𝘋𝘌𝘍𝘎𝘏𝘐𝘑𝘒𝘓𝘔𝘕𝘖𝘗𝘘𝘙𝘚𝘛𝘜𝘝𝘞𝘟𝘠𝘡𝘢𝘣𝘤𝘥𝘦𝘧𝘨𝘩𝘪𝘫𝘬𝘭𝘮𝘯𝘰𝘱𝘲𝘳𝘴𝘵𝘶𝘷𝘸𝘹𝘺𝘻𝛢𝛣𝛤𝛥𝛦𝛧𝛨𝛩𝛪𝛫𝛬𝛭𝛮𝛯𝛰𝛱𝛲𝛳𝛴𝛵𝛶𝛷𝛸𝛹𝛺𝛻𝛼𝛽𝛾𝛿𝜀𝜁𝜂𝜃𝜄𝜅𝜆𝜇𝜈𝜉𝜊𝜋𝜌𝜍𝜎𝜏𝜐𝜑𝜒𝜓𝜔𝜕𝜖𝜗𝜘𝜙𝜚𝜛"));
        }

        private void BScript_Click(object sender, EventArgs e)
        {
            SetStringSelection(Xlit(GetStringSelecion(),
                "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnoprstuvwxyz",
                "𝒜ℬ𝒞𝒟ℰℱ𝒢ℋℐ𝒥𝒦ℒℳ𝒩𝒪𝒫𝒬ℛ𝒮𝒯𝒰𝒱𝒲𝒳𝒴𝒵𝒶𝒷𝒸𝒹ℯ𝒻ℊ𝒽𝒾𝒿𝓀𝓁𝓂𝓃ℴ𝓅𝓆𝓇𝓈𝓉𝓊𝓋𝓌𝓍𝓎𝓏"));
        }

        private void BSelectChar_Click(object sender, EventArgs e)
        {
            int selStart = GetStringSelectionStart();
            if (tbChars.Checked)
            {
                etString.Text = etString.Text.Substring(0, selStart)
                    + tvCharPreview.Text
                    + etString.Text.Substring(selStart + selLength);
            }
            else
            {
                etString.Text = etString.Text.Substring(0, GetStringSelectionStart())
                    + tvCharPreview.Text
                    + etString.Text.Substring(GetStringSelectionEnd());
            }
            etString.SetSelection(selStart + tvCharPreview.Text.Length);
        }

        private void BSelectChar_LongClick(object sender, View.LongClickEventArgs e)
        {
            if (tbChars.Checked ? tvChar.Text == "" : etString.SelectionStart == etString.SelectionEnd)
            {
                return;
            }

            int end = Convert.ToInt32(UnicodeEncode(tvCharPreview.Text)[0], 16), start;
            string s = "";
            if (tbChars.Checked)
            {
                start = Convert.ToInt32(UnicodeEncode(tvChar.Text[0])[0], 16);
            }
            else
            {
                start = Convert.ToInt32(UnicodeEncode(GetStringSelecion())[0], 16);
            }
            for (int i = start; start <= end ? i <= end : i >= end; i += start <= end ? 1 : -1)
            {
                s += UnicodeDecode(i);
            }
            int selStart = GetStringSelectionStart();
            if (tbChars.Checked)
            {
                etString.Text = etString.Text.Substring(0, selStart)
                    + s
                    + etString.Text.Substring(selStart + selLength);
            }
            else
            {
                etString.Text = etString.Text.Substring(0, GetStringSelectionStart())
                    + s
                    + etString.Text.Substring(GetStringSelectionEnd());
            }
            etString.SetSelection(selStart + s.Length);
        }

        private void BSmall_Click(object sender, EventArgs e)
        {
            SetStringSelection(Xlit(GetStringSelecion(), "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnoprstuvwxyz", "ᴀʙᴄᴅᴇꜰɢʜɪᴊᴋʟᴍɴᴏᴘꞯʀꜱᴛᴜᴠᴡxʏᴢᵃᵇᶜᵈᵉᶠᵍʰⁱʲᵏˡᵐⁿᵒᵖʳˢᵗᵘⱽʷˣʸᶻ"));
        }

        private void BStrikethrough_Click(object sender, EventArgs e)
        {
            etString.Text = Combine(etString.Text, "̶");
        }

        private string String(string s, int n)
        {
            string r = "";
            for (int i = 1; i <= n; i++)
            {
                r += s;
            }
            return r;
        }

        private void BString_Click(object sender, EventArgs e)
        {
            etString.Text = String(etString.Text, int.Parse(etNumber.Text));
        }

        private void BString2_Click(object sender, EventArgs e)
        {
            etString.Text = String(etString.Text, 2);
        }

        private void BString3_Click(object sender, EventArgs e)
        {
            etString.Text = String(etString.Text, 3);
        }

        private void BSubscript(object sender, EventArgs e)
        {
            SetStringSelection(Xlit(GetStringSelecion(), "0123456789+-=()aehijklmnoprstuvx", "₀₁₂₃₄₅₆₇₈₉₊₋₌₍₎ₐₑₕᵢⱼₖₗₘₙₒₚᵣₛₜᵤᵥₓ"));
        }

        private void BSuperscript(object sender, EventArgs e)
        {
            SetStringSelection(Xlit(GetStringSelecion(), "0123456789+-=()ABDEGHIJKLMNOPRTUWabcdefghijklmnoprstuvwxyz", "⁰¹²³⁴⁵⁶⁷⁸⁹⁺⁻⁼⁽⁾ᴬᴮᴰᴱᴳᴴᴵᴶᴷᴸᴹᴺᴼᴾᴿᵀᵁᵂᵃᵇᶜᵈᵉᶠᵍʰⁱʲᵏˡᵐⁿᵒᵖʳˢᵗᵘᵛʷˣʸᶻ"));
        }

        private void BTurned_Click(object sender, EventArgs e)
        {
            SetStringSelection(Xlit(GetStringSelecion(), "ABCDEFGHIJKLMNOPRSTUVWXYZ", "ꓯꓭꓛꓷꓱꓞꓨꓧꓲꓩꓘꓶꓪꓠꓳꓒꓤꓢꓕꓵꓥꓟꓫ⅄ꓜ"));
        }

        private void BUnderline_Click(object sender, EventArgs e)
        {
            etString.Text = Combine(etString.Text, "̲");
        }

        private void BUnicodeEncode_Click(object sender, EventArgs e)
        {
            string[] u;
            if (tbChars.Checked)
            {
                u = UnicodeEncode(tvChar.Text);
            }
            else
            {
                u = UnicodeEncode(etString.Text);
            }
            llCode.RemoveAllViews();
            TextView text;
            foreach (string s in u)
            {
                LinearLayout layout = new LinearLayout(this);
                text = new TextView(this)
                {
                    Text = "\n　" + UnicodeDecode(Convert.ToInt32(s, 16)) + "　\n"
                };
#pragma warning disable CS0618 // 类型或成员已过时
                text.SetTextAppearance(this, Resource.Style.TextAppearance_AppCompat_Large);
#pragma warning restore CS0618 // 类型或成员已过时
                layout.AddView(text);
                text = new TextView(this)
                {
                    Text = "\nU+" + s
                };
                layout.AddView(text);
                llCode.AddView(layout);
                text = new TextView(this)
                {
                    Text = "　" + GetCharDescription(Convert.ToInt32(s, 16))
                };
                llCode.AddView(text);
            }
        }

        private void BUpper_Click(object sender, EventArgs e)
        {
            SetStringSelection(GetStringSelecion().ToUpper());
        }

        private string CleanSymbols(string s)
        {
            string CJKUIs = "";
            foreach (char c in s)
            {
                if (IsCJKUI(c))
                {
                    CJKUIs += c;
                }
            }
            return CJKUIs;
        }

        private string Combine(string s, string combiningChar)
        {
            string combined = "";
            foreach (char c in s)
            {
                combined += c + combiningChar;
            }
            return combined;
        }

        private void EtChar_KeyPress(object sender, View.KeyEventArgs e)
        {
            e.Handled = false;
            if (e.KeyCode != Keycode.Enter)
            {
                return;
            }
            etString.Text = etString.Text.Substring(0, selStart) + etChar.Text + etString.Text.Substring(selStart + selLength);
            etChar.Visibility = ViewStates.Gone;
            tvChar.Visibility = ViewStates.Visible;
            etChar.Text = "";
        }

        private void EtString_TextChanged(object sender, Android.Text.TextChangedEventArgs e)
        {
            if (tbChars.Checked)
            {
                SelectChars();
            }
            else
            {
                tvStatus.Text = etString.Text.Length.ToString();
            }
            if (tabHost.CurrentTabTag == "symbols")
            {
                RefreshSymbolsSb();
            }
        }

        private string GetCharDescription(char c)
        {
            return GetCharDescription(Convert.ToInt32(UnicodeEncode(c)[0], 16));
        }

        private string GetCharDescription(int i)
        {
            if (descriptionSource == 0)
                return tvCharDescription.Text;
            string d = "";
            if (i < 0x4000 || 0xa000 <= i && i < 0xe000 || 0xf000 <= i && i < 0x20000)
            {
                d = descriptions[i];
                if (d == null)
                {
                    d = "字符描述加載中..." + progress;
                }
                else
                {
                    d = d
                        .Replace("&lt;control&gt;", "<控制>")
                        .Replace("&#", "");
                }
            }
            return d;
        }

        public string GetHTML(string url)
        {
            return GetHTML(url, "UTF-8");
        }

        public string GetHTML(string url, string encodingName)
        {
            WebClient wc = new WebClient
            {
                Encoding = Encoding.GetEncoding(encodingName)
            };
            try
            {
                return wc.DownloadString(url);
            }
            catch (Exception)
            {
            }
            return "";
        }

        private string GetString()
        {
            return tbChars.Checked ? tvChar.Text : etString.Text;
        }

        private string GetStringSelecion()
        {
            if (etString.HasSelection)
            {
                return etString.Text.Substring(GetStringSelectionStart(), GetStringSelectionLength());
            }
            else
            {
                return etString.Text;
            }
        }

        private int GetStringSelectionLength()
        {
            return Math.Abs(etString.SelectionEnd - etString.SelectionStart);
        }

        private int GetStringSelectionEnd()
        {
            return Math.Max(etString.SelectionStart, etString.SelectionEnd);
        }

        private int GetStringSelectionStart()
        {
            return Math.Min(etString.SelectionStart, etString.SelectionEnd);
        }

        private bool IsCJKUI(char c)
        {
            return '0' <= c && c <= '9'
                || 'A' <= c && c <= 'Z'
                || 'a' <= c && c <= 'z'
                || '䀀' <= c && c <= '䶿'
                || '一' <= c && c <= '鿿'
                || '豈' <= c && c <= '﫿';
        }

        private void IvCharmap_Touch(object sender, View.TouchEventArgs e)
        {
            int i = (int)(first + Math.Floor(e.Event.GetY() / ivCharmap.Height * 16) * 16 + Math.Floor(e.Event.GetX() / ivCharmap.Width * 16));
            tvCharPreview.Text = UnicodeDecode(i);
            tvCharUnicode.Text = "U+" + i.ToString("X");
            tvCharDescription.Text = GetCharDescription(i);
        }

        private void LoadDescription()
        {
            switch (descriptionSource)
            {
                case 1:
                    string html = "";
                    for (int i = 0; i < 0x20; i++)
                    {
                        if (0x4 <= i && i < 0xa || i == 0xe)
                        {
                            continue;
                        }
                        html = "";
                        progress = i.ToString("X") + "000 ~ " + i.ToString("X") + "FFF [0/2]";
                        try
                        {
                            html = GetHTML("https://en.wikibooks.org/wiki/Unicode/Character_reference/" + i.ToString("X") + "000-" + i.ToString("X") + "FFF");
                            progress = " (" + i.ToString("X") + "000 ~ " + i.ToString("X") + "FFF [1/2])";
                            html.Substring(html.IndexOf("<td colspan=\"17\" style=\"background:#f8f8f8;text-align:center\"><b>"));
                            html.Substring(0, html.IndexOf("</th></tr></tbody>") + 10);
                            string[] s = Regex.Matches(html,
                                "(?<=<td><span title=\").+(?=\" style=\"cursor:help;\" id=\"title\" class=\"htitle\">)|" +
                                "(?<=<td style=\"background:#[0-9a-f]{6}\"><span title=\").+(?=\" style=\"cursor:help;\" id=\"title\" class=\"htitle\">)|" +
                                "(?<=<td style=\"font-size:75%\"><span title=\").+(?=\" style=\"cursor:help;\" id=\"title\" class=\"htitle\">)|" +
                                "(?<=<td style=\"background:#[0-9a-f]{6};font-size:75%\"><span title=\").+(?=\" style=\"cursor:help;\" id=\"title\" class=\"htitle\">)|" +
                                "(?<=<td>)&#(?=.+;\n?</td>)|" +
                                "(?<=<td style=\"background:#777777\">)&#(?=.+;\n?</td>)"
                                ).Cast<Match>().Select(m => m.Value).ToArray();
                            s.CopyTo(descriptions, i * 0x1000);
                        }
                        catch (Exception)
                        {
                        }
                        progress = "";
                    }
                    break;
                case 2:
                    progress = "";
                    html = GetHTML("https://www.unicode.org/charts/charindex.html");
                    string[] lines = html.Split('\n');
                    foreach (string line in lines)
                    {
                        if (Regex.IsMatch(line, "<tr><td>[ ,0-9A-Za-z]+</td><td><a href=\"PDF/U[0-9A-F]{4,5}\\.pdf\">[0-9A-F]{4,5}</a></td></tr>"))
                        {
                            int i = Convert.ToInt32(Regex.Match(line, "(?<=\\.pdf\">)[0-9A-F]{4,5}(?=</a></td></tr>)").Value, 16);
                            if (i < 0x20000)
                                descriptions[i] = Regex.Match(line, "(?<=<tr><td>)[ ,0-9A-Za-z]+(?=</td><td><a href=\"PDF/U)").Value;
                        }
                    }
                    break;
            }
        }

        private void SbSymbols_ProgressChanged(object sender, SeekBar.ProgressChangedEventArgs e)
        {
            SeekBar sb = (SeekBar)sender;
            if (sb.Progress < lastSymbols)
            {
                SymbolsLeft();
            }
            if (sb.Progress > lastSymbols)
            {
                SymbolsRight();
            }
            lastSymbols = sb.Progress;
        }

        private void RefreshSymbolsSb()
        {
            sbSymbols.ProgressChanged -= SbSymbols_ProgressChanged;
            int left = 0, right = 0;
            for (int i = etString.Text.Length - 1; i >= 0; i--)
            {
                if (!IsCJKUI(etString.Text[i]))
                {
                    left = i;
                    break;
                }
            }
            for (int i = 0; i < etString.Text.Length; i++)
            {
                if (!IsCJKUI(etString.Text[i]))
                {
                    right = etString.Text.Length - i - 1;
                    break;
                }
            }
            sbSymbols.Max = left + right;
            sbSymbols.Progress = left;
            lastSymbols = sbSymbols.Progress;
            sbSymbols.ProgressChanged += SbSymbols_ProgressChanged;
        }

        private void SelectChars()
        {
            if (etString.Text == "")
            {
                tvPrevChar.Text = "";
                tvChar.Text = "";
                tvNextChar.Text = "";
                return;
            }
            if (selStart + selLength > etString.Text.Length)
            {
                selStart = etString.Text.Length - 1;
                selLength = 1;
            }
            if (selStart > 0)
            {
                tvPrevChar.Text = etString.Text[selStart - 1].ToString();
            }
            else
            {
                tvPrevChar.Text = "";
            }
            tvChar.Text = etString.Text.Substring(selStart, selLength);
            if (selStart + selLength < etString.Text.Length)
            {
                tvNextChar.Text = etString.Text[selStart + selLength].ToString();
            }
            else
            {
                tvNextChar.Text = "";
            }
            tvCharDescription.Text = GetCharDescription(tvChar.Text[^1]);
            if (tbSelect.Checked)
            {
                tvStatus.Text = selStart + 1 + " ~ " + (selStart + selLength);
            }
            else
            {
                tvStatus.Text = (selStart + 1).ToString();
            }
        }

        private void SetCharmapSpinnerSelection()
        {
            for (int i = 0; i < sCharmap.Adapter.Count; i++)
            {
                string[] obj = sCharmap.GetItemAtPosition(i).ToString().Replace(" ", "").Split('~');
                if (Convert.ToInt32(obj[0], 16) <= first && first <= Convert.ToInt32(obj[1], 16))
                {
                    sCharmap.SetSelection(i);
                    break;
                }
            }
        }

        private void SetString(string s)
        {
            if (tbChars.Checked)
            {
                etString.Text = etString.Text.Substring(0, selStart) + s + etString.Text.Substring(selStart + selLength);
                SelectChars();
            }
            else
            {
                etString.Text = s;
            }
        }

        private void SetStringSelection(string s)
        {
            if (etString.HasSelection)
            {
                etString.Text = etString.Text.Substring(0, GetStringSelectionStart()) + s + etString.Text.Substring(GetStringSelectionEnd());
            }
            else
            {
                etString.Text = s;
            }
        }

        private void ShowCharsOnCharmap()
        {
            canvas.DrawColor(Color.Transparent, Mode.Clear);
            ivCharmap.SetImageBitmap(bitmap);
            int charWidth = llCharmap.Width / 16, charHeight = llCharmap.Height / 16;
            for (int i = first; i < first + 256; i++)
            {
                canvas.DrawText(UnicodeDecode(i), i % 16 * charWidth + charWidth / 2, (float)Math.Floor((double)(i % 256 / 16 * charHeight)) + charHeight / 2, paint);
            }
        }

        private void SpinnerCharmap_ItemSelected(object sender, AdapterView.ItemSelectedEventArgs e)
        {
            string[] s = sCharmap.GetItemAtPosition(e.Position).ToString().Replace(" ", "").Split('~');
            if (first < Convert.ToInt32(s[0], 16) || Convert.ToInt32(s[1], 16) < first)
            {
                first = Convert.ToInt32(s[0], 16);
                ShowCharsOnCharmap();
            }
        }

        private void SymbolsLeft()
        {
            if (etString.Text == "")
            {
                return;
            }
            string c = CleanSymbols(etString.Text), s = etString.Text;
            for (int i = 1; i < s.Length; i++)
            {
                if (!IsCJKUI(char.Parse(s.Substring(i, 1))))
                {
                    c = c.Substring(0, i - 1) + s.Substring(i, 1) + c.Substring(i - 1);
                }
            }
            etString.Text = c;
        }

        private void SymbolsRight()
        {
            if (etString.Text == "")
            {
                return;
            }
            string c = CleanSymbols(etString.Text), s = etString.Text;
            for (int i = 0; i < s.Length - 1; i++)
            {
                if (!IsCJKUI(char.Parse(s.Substring(i, 1))))
                {
                    if (i < c.Length)
                    {
                        c = c.Substring(0, i + 1) + s.Substring(i, 1) + c.Substring(i + 1);
                    }
                    else
                    {
                        c += s.Substring(i, 1);
                    }
                }
            }
            etString.Text = c;
        }

        private void TabHost_TabChanged(object sender, TabHost.TabChangeEventArgs e)
        {
            switch (e.TabId)
            {
                case "symbols":
                    RefreshSymbolsSb();
                    break;
                case "charmap":
                    tvCharDescription.Text = "";
                    break;
            }
            if (e.TabId != "charmap")
            {
                if (tbChars.Checked && tvChar.Text != "")
                {
                    tvCharDescription.Text = GetCharDescription(tvChar.Text[0]);
                }
            }
        }

        private void TvChars_Click(object sender, EventArgs e)
        {
            if (tvChar.Text == "")
            {
                return;
            }
            etChar.Text = tvChar.Text;
            etChar.SetSelection(0, etChar.Text.Length);
            tvChar.Visibility = ViewStates.Gone;
            etChar.Visibility = ViewStates.Visible;
            etChar.RequestFocus();
        }

        private void TvChar_LongClick(object sender, View.LongClickEventArgs e)
        {
            selStart = etString.SelectionStart;
            selLength = 1;
            SelectChars();
        }

        private void TbChars_CheckedChange(object sender, CompoundButton.CheckedChangeEventArgs e)
        {
            if (e.IsChecked)
            {
                FindViewById<LinearLayout>(Resource.Id.ll_chars).Visibility = ViewStates.Visible;
                tbSelect.Visibility = ViewStates.Visible;
                SelectChars();
            }
            else
            {
                tbSelect.Visibility = ViewStates.Gone;
                FindViewById<LinearLayout>(Resource.Id.ll_chars).Visibility = ViewStates.Gone;
                tvCharDescription.Text = "";
                tvStatus.Text = etString.Text.Length.ToString();
            }
        }

        private void TbSelect_CheckedChange(object sender, CompoundButton.CheckedChangeEventArgs e)
        {
            if (!e.IsChecked)
            {
                selLength = 1;
            }
        }

        private string UnicodeDecode(int unicode)
        {
            string bin = Convert.ToString(unicode, 2);
            List<byte> bytes = new List<byte>();
            if (unicode <= 0x7f)
            {
                bytes.Add(Convert.ToByte("0" + bin.PadLeft(7, '0'), 2));
            }
            else
            {
                byte length = 0;
                if (0x80 <= unicode && unicode <= 0x7ff)
                {
                    length = 2;
                }
                else if (0x800 <= unicode && unicode <= 0xffff)
                {
                    length = 3;
                }
                else if (0x10000 <= unicode && unicode <= 0x1fffff)
                {
                    length = 4;
                }
                else if (0x200000 <= unicode && unicode <= 0x3ffffff)
                {
                    length = 5;
                }
                else if (0x4000000 <= unicode && unicode <= 0x7fffffff)
                {
                    length = 6;
                }
                bin = bin.PadLeft(length * 5 + 1, '0');
                bytes.Add(Convert.ToByte("0".PadLeft(length + 1, '1') + bin.Substring(0, 7 - length), 2));
                bin = bin.Substring(7 - length);
                for (int i = 0; i < length - 1; i++)
                {
                    bytes.Add(Convert.ToByte("10" + bin.Substring(i * 6, 6), 2));
                }
            }
            return string.Join("", Encoding.UTF8.GetChars(bytes.ToArray()));
        }

        private string[] UnicodeEncode(char c)
        {
            return UnicodeEncode(c.ToString());
        }

        private string[] UnicodeEncode(string s)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(s);
            List<string> unicodes = new List<string>();
            foreach (byte b in bytes)
            {
                string bin = Convert.ToString(b, 2).PadLeft(8, '0');
                if (bin.Substring(0, 1) == "0")
                {
                    unicodes.Add(bin.Substring(1));
                }
                else if (bin.Substring(0, 2) == "10")
                {
                    unicodes[^1] += bin.Substring(2);
                }
                else
                {
                    unicodes.Add(bin.Substring(bin.IndexOf('0') + 1));
                }
            }
            for (int i = 0; i < unicodes.Count; i++)
            {
                unicodes[i] = Convert.ToInt32(unicodes[i], 2).ToString("X");
            }
            return unicodes.ToArray();
        }

        private string Xlit(string s, string oldString, string newString)
        {
            if (oldString.Length == newString.Length)
            {
                for (int i = 0; i < oldString.Length; i++)
                {
                    s = s.Replace(oldString.Substring(i, 1), newString.Substring(i, 1));
                }
            }
            return s;
        }
    }
}