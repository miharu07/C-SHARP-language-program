﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Speech.Synthesis;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using 翻译神器.SourceOfTranslation;

namespace 翻译神器
{
    // 百度文字识别和百度翻译key
    public struct BaiduKey
    {
        public static string ApiKey;
        public static string SecretKey;
        public static string AppId;
        public static string Password;
        public static bool IsEmptyOrNull
        {
            get
            {
                if (string.IsNullOrEmpty(ApiKey) && string.IsNullOrEmpty(SecretKey)
                    && string.IsNullOrEmpty(AppId) && string.IsNullOrEmpty(Password))
                    return true;
                else
                    return false;
            }
        }
    }

    // 有道文字识别key
    public struct YoudaoKey
    {
        public static string AppKey;
        public static string AppSecret;
        public static bool IsEmptyOrNull
        {
            get
            {
                if (string.IsNullOrEmpty(AppKey) && string.IsNullOrEmpty(AppSecret))
                    return true;
                else
                    return false;
            }
        }
    }

    public partial class FrmMain : Form
    {
        // **************************************主窗口**************************************

        public FrmMain()
        {
            InitializeComponent();

            // 显示窗口
            MenuItem show = new MenuItem("显示");
            show.Click += new EventHandler(ShowWin);
            // 隐藏窗口
            MenuItem hide = new MenuItem("隐藏");
            hide.Click += new EventHandler(HideWin);
            // 退出菜单项  
            MenuItem exit = new MenuItem("退出");
            exit.Click += new EventHandler(Exit);
            // 关联托盘控件  
            MenuItem[] childen = new MenuItem[] { show, hide, exit };
            notifyIcon1.ContextMenu = new ContextMenu(childen);
            notifyIcon1.Text = "点击此处显示窗口";

            this.WindowState = FormWindowState.Minimized;
            this.ShowInTaskbar = false;
            comboBox_TranSource.SelectedIndex = 0;
            InitDictionary();
        }


        private Dictionary<string, string> config = new Dictionary<string, string>(16);
        private Rectangle screenRect = new Rectangle();// 固定截图坐标
        private bool isEnToZh = true;                // 翻译模式 英译中、俄译中
        private bool isSpeak;                        // 翻译后是否朗读译文
        private bool isFixedScreen;                  // 是否为固定截图翻译
        private bool copySourceTextToClip, copyDestTextToClip;     // 翻译后是否复制到剪切板（源语言、目标语言）
        private bool putOnTheHook;                   // 装上钩子
        private int showTime = 5;                    // 翻译后延迟显示翻译后内容的时间（单位：秒）
        private string sourceOfTran = "百度翻译";    // 翻译来源（可选 百度 或 有道）
        private DateTime lastTime = DateTime.Now;    // 记录上次热键按下时间，避免多次按下热键造成卡死闪退
        private Thread newThread;                    // 新线程              
        private string showCont;                     // 要在ShowCont窗口显示的内容
        private TranMode tranMode;                   // 翻译模式（截图翻译并显示 或 不截图翻译只显示）
        private string windowName, windowClass;
        const int HOT_KEY_NUM = 5;                    // 要注册的热键个数


        private void InitDictionary(bool changeBaiduKey = false, bool changeYoudaoKey = false)
        {
            BaiduKey.ApiKey = config[label_BaiduApiKey.Text] = textBox_BaiduApiKey.Text;
            BaiduKey.SecretKey = config[label_BaiduSecretKey.Text] = textBox_BaiduSecretKey.Text;
            BaiduKey.AppId = config[label_BaiduAppId.Text] = textBox_BaiduAppId.Text;
            BaiduKey.Password = config[label_BaiduPassword.Text] = textBox_BaiduPassword.Text;
            if (changeBaiduKey)
                return;

            YoudaoKey.AppKey = config[label_YoudaoAppKey.Text] = textBox_YoudaoAppKey.Text;
            YoudaoKey.AppSecret = config[label_YoudaoAppSecret.Text] = textBox_YoudaoAppSecret.Text;
            if (changeYoudaoKey)
                return;

            config[label_ScreenHotkey.Text] = textBox_ScreenHotkey.Text;   // 截图热键
            config[label_SwitchEnToCn.Text] = textBox_SwitchEnToCn.Text;   // 切换英中
            config[label_TranHotkey.Text] = textBox_TranHotkey.Text;       // 翻译热键
            config[label_SwitchRuToCn.Text] = textBox_SwitchRuToCn.Text;   // 切换俄中
            config[label_FixedTranHotkey.Text] = textBox_FixedTranHotkey.Text;         // 固定翻译
            config[label_Delay.Text] = numericUpDown_Delay.Value.ToString();// 延迟
            config[checkBox_CopyOriginalText.Text] = checkBox_CopyOriginalText.Checked.ToString();// 复制翻译原文 
            config[checkBox_CopyTranText.Text] = checkBox_CopyTranText.Checked.ToString();        // 复制翻译译文
            config[checkBox_ReadAloud.Text] = checkBox_ReadAloud.Checked.ToString();                 // 翻译后朗读译文
            config[label_TranSource.Text] = comboBox_TranSource.Text;//翻译来源 
            config["固定截图X坐标"] = screenRect.X.ToString();      // 固定翻译截图横坐标
            config["固定截图Y坐标"] = screenRect.Y.ToString();      // 固定翻译截图竖坐标
            config["固定截图宽度"] = screenRect.Width.ToString();   // 固定翻译截图宽度
            config["固定截图高度"] = screenRect.Height.ToString();  // 固定翻译截图高度
            config[label_WindowName.Text] = textBox_WindowName.Text;  // 窗口标题
            config[label_WindowClass.Text] = textBox_WindowClass.Text;// 固定翻译类名
        }


        // 翻译模式（tran：截图翻译并显示，show：不截图翻译只显示）
        enum TranMode
        {
            TranAndShowText,
            ShowText,
        }


        private void ShowWin(object sender, EventArgs e)
        {
            this.Show();
            this.WindowState = FormWindowState.Normal; // 窗体恢复正常大小
            this.ShowInTaskbar = true;
        }


        private void HideWin(object sender, EventArgs e)
        {
            this.Hide();
            this.ShowInTaskbar = false;
        }


        private void Exit(object sender, EventArgs e)
        {
            UnRegHotKey();
            CloseThread(false);     // 关闭线程
            notifyIcon1.Dispose();  // 释放notifyIcon1的所有资源，保证托盘图标在程序关闭时立即消失
            this.Dispose();
            Environment.Exit(0);    // 退出
        }


        private void FrmMain_Load(object sender, EventArgs e)
        {
            LoadFile();
        }

        private void LoadFile(bool reload = false)
        {
            // 判断文件是否存在
            if (!File.Exists(ConfigFile.ConfigPath))
            {
                MessageBox.Show("请先设定热键！", "截图翻译", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                // 读取配置文件
                ConfigFile.ReadFile(ref config);
                DataRecovery();
                if (reload)// 重新加载配置文件
                    UnRegHotKey();
                RegHotKey();
            }
            catch (Exception ex)
            {
                MessageBox.Show("加载配置文件错误，请重新设置！", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                if (BaiduKey.IsEmptyOrNull)
                {
                    BaiduKey.ApiKey = "AznG9zhnWiW1HX0MjwA0hMVX";
                    BaiduKey.SecretKey = "qq2LcLeS6hm3aydfkko14AfeVGo2lSUq";
                    BaiduKey.AppId = "20200424000429104";
                    BaiduKey.Password = "5mzyraBsLRk2yfGQMhXJ";
                }
            }
        }


        // 从字典config恢复数据
        private void DataRecovery()
        {
            // 翻译后延迟显示的时间（秒）
            showTime = Convert.ToInt32(config[label_Delay.Text]);
            // 翻译后是否复制到剪切板
            copySourceTextToClip = Convert.ToBoolean(config[checkBox_CopyOriginalText.Text]);
            copyDestTextToClip = Convert.ToBoolean(config[checkBox_CopyTranText.Text]);
            // 翻译后是否朗读译文
            isSpeak = Convert.ToBoolean(config[checkBox_ReadAloud.Text]);
            // 固定截图翻译坐标
            screenRect.X = Convert.ToInt32(config["固定截图X坐标"]);
            screenRect.Y = Convert.ToInt32(config["固定截图Y坐标"]);
            screenRect.Width = Convert.ToInt32(config["固定截图宽度"]);
            screenRect.Height = Convert.ToInt32(config["固定截图高度"]);

            // 恢复数据到窗口
            textBox_ScreenHotkey.Text = config[label_ScreenHotkey.Text];
            textBox_TranHotkey.Text = config[label_TranHotkey.Text];
            textBox_FixedTranHotkey.Text = config[label_FixedTranHotkey.Text];
            textBox_SwitchEnToCn.Text = config[label_SwitchEnToCn.Text];
            textBox_SwitchRuToCn.Text = config[label_SwitchRuToCn.Text];

            numericUpDown_Delay.Value = Convert.ToDecimal(config[label_Delay.Text]);

            checkBox_CopyOriginalText.Checked = Convert.ToBoolean(config[checkBox_CopyOriginalText.Text]);
            checkBox_CopyTranText.Checked = Convert.ToBoolean(config[checkBox_CopyTranText.Text]);
            checkBox_ReadAloud.Checked = Convert.ToBoolean(config[checkBox_ReadAloud.Text]);
            // 翻译源
            sourceOfTran = (string)(comboBox_TranSource.SelectedItem = config[label_TranSource.Text]);

            // 固定翻译窗口标题或类名
            windowName = textBox_WindowName.Text = config[label_WindowName.Text];
            windowClass = textBox_WindowClass.Text = config[label_WindowClass.Text];

            BaiduKey.ApiKey = textBox_BaiduApiKey.Text = config[label_BaiduApiKey.Text];
            BaiduKey.SecretKey = textBox_BaiduSecretKey.Text = config[label_BaiduSecretKey.Text];
            BaiduKey.AppId = textBox_BaiduAppId.Text = config[label_BaiduAppId.Text];
            BaiduKey.Password = textBox_BaiduPassword.Text = config[label_BaiduPassword.Text];

            YoudaoKey.AppKey = textBox_YoudaoAppKey.Text = config[label_YoudaoAppKey.Text];
            YoudaoKey.AppSecret = textBox_YoudaoAppSecret.Text = config[label_YoudaoAppSecret.Text];
        }


        // 获取功能键键值
        private uint GetKeyVal(string key)
        {
            switch (key.Trim().ToLower())
            {
                case "alt":
                    return 0x0001;
                case "control":
                    return 0x0002;
                case "shift":
                    return 0x0004;
                case "win":
                    return 0x0008;
            }
            return 0;
        }

        private void RegHotKey()
        {
            // 注册热键
            int i = 0, j = 0, id;
            string hotKey;
            Keys key;
            uint fun1 = 0, fun2 = 0;

            foreach (var item in config)
            {
                if (j++ < 6)
                    continue;
                if (i > 4)
                    break;

                hotKey = item.Value;
                id = 1000 + i;
                i++;
                // 如果热键为空
                if (string.IsNullOrEmpty(hotKey))
                    continue;

                string[] arr = hotKey.Split(',');
                // 前一个键为单键（a-z）
                key = (Keys)Enum.Parse(typeof(Keys), arr[0], true);
                if (arr.Length == 3) // 三个键
                {
                    // 后两个键为功能键（ctrl、alt...）
                    fun1 = GetKeyVal(arr[1]);
                    fun2 = GetKeyVal(arr[2]);
                }
                if (arr.Length == 2)// 两个键
                {
                    // 后一个键为功能键（ctrl、alt...）
                    fun1 = GetKeyVal(arr[1]);
                }
                // 如果只有一个键就取第一个键就好了key = arr[0];
                if (!Api.RegisterHotKey(this.Handle, id, fun1 | fun2, key)) // 注册热键
                {   // 如果注册失败
                    throw new Exception("注册热键失败！");
                }
            }
            putOnTheHook = true;
        }


        private void UnRegHotKey()
        {
            for (int i = 0; i < HOT_KEY_NUM; i++)
            {
                Api.UnregisterHotKey(this.Handle, 1000 + i); // 卸载热键
            }
            putOnTheHook = false;
        }

        // 通过监视系统消息，判断是否按下热键
        protected override void WndProc(ref Message m)
        {
            if (m.Msg != 0x0312)                    // 如果m.Msg的值为0x0312那么表示用户按下了热键
            {
                base.WndProc(ref m);
                return;
            }

            switch (m.WParam.ToString())
            {
                case "1000": // 截图翻译
                    {
                        isFixedScreen = false;
                        tranMode = TranMode.TranAndShowText;
                        StartThread(null);
                        break;
                    }
                case "1001": // 切换英译中模式
                    {
                        if (!isEnToZh) // 如果当前翻译模式为 俄 译 中
                        {
                            showCont = "当前翻译模式：英译中";
                            isEnToZh = true;
                            tranMode = TranMode.ShowText; // 显示“当前模式：英译中”这句话
                            StartThread(showCont);
                        }
                        break;
                    }
                case "1002": // 翻译
                    {
                        ShowTranForm();
                        break;
                    }
                case "1003": // 切换俄译中模式
                    {
                        if (isEnToZh) // 如果当前翻译模式为 英 译 中
                        {
                            showCont = "当前翻译模式：俄译中";
                            isEnToZh = false;
                            tranMode = TranMode.ShowText;  // 显示“当前模式：x译中”这句话
                            StartThread(showCont);
                        }
                        break;
                    }
                case "1004": // 固定区域截图翻译
                    {
                        isFixedScreen = true;
                        tranMode = TranMode.TranAndShowText;
                        StartThread(null);
                        break;
                    }
            }

            base.WndProc(ref m);
        }


        private void FrmMain_FormClosed(object sender, FormClosedEventArgs e)
        {
            Exit(null, null);
        }


        private void checkBox_Click(object sender, EventArgs e)
        {
            if (!((CheckBox)sender).Checked)
                ((CheckBox)sender).Checked = true;
            else
                ((CheckBox)sender).Checked = false;
        }


        private void button_Save_Click(object sender, EventArgs e)
        {
            try
            {
                List<string> list = new List<string>();
                foreach (Control ctl in groupBox_HotKey.Controls)
                {
                    // 如果控件类型不是TextBox
                    if (!(ctl is TextBox) || string.IsNullOrEmpty(ctl.Text))
                        continue;
                    list.Add(ctl.Text);
                }

                InitDictionary();
                ConfigFile.WriteFile(config);
                MessageBox.Show("保存成功，立即生效！", "保存成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                LoadFile(true);
            }
            catch (Exception ex)
            {
                MessageBox.Show("保存失败！\n原因：" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }


        private void textBox_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
        {
            // 将按下的键显示到text
            ((TextBox)sender).Text = e.KeyData.ToString();
        }


        // 设置固定翻译坐标
        private void button_SetPosition_Click(object sender, EventArgs e)
        {
            FrmScreenShot shot = new FrmScreenShot();
            try
            {
                if (string.IsNullOrEmpty(textBox_WindowClass.Text) && string.IsNullOrEmpty(textBox_WindowName.Text))
                    throw new Exception("\n请填写 窗口标题 或 窗口类名！");
                else if (!string.IsNullOrEmpty(textBox_WindowClass.Text) && !string.IsNullOrEmpty(textBox_WindowName.Text))
                    throw new Exception("\n请不要同时填写 窗口标题 或 窗口类名！");
                else
                {
                    windowName = textBox_WindowName.Text;
                    windowClass = textBox_WindowClass.Text;
                }

                IntPtr hwnd = FindWindowHandle();
                MinimizeWindow(true);
                Thread.Sleep(200);
                Api.SetForegroundWindow(hwnd);
                Thread.Sleep(200);

                shot.WindowHandle = hwnd;
                shot.ScreenImage = shot.CopyScreen();
                // 显示截图窗口
                DialogResult result = shot.ShowDialog();
                if (result == DialogResult.Cancel)
                    throw new Exception("用户取消截图！");

                Api.POINT p = new Api.POINT();
                p.X = shot.StartPos.X;
                p.Y = shot.StartPos.Y;

                // 屏幕坐标转为客户端窗口坐标
                Api.ScreenToClient(hwnd, ref p);
                // 保存截图坐标高宽
                screenRect = new Rectangle(p.X, p.Y, shot.SelectedArea.Width, shot.SelectedArea.Height);
                if (shot.SelectedArea.Width > 0 && shot.SelectedArea.Height > 0)
                    MessageBox.Show("设置成功！\n请点击“保存配置”按钮。", "截图翻译", MessageBoxButtons.OK, MessageBoxIcon.Information);
                else
                    throw new Exception("设置失败，请重试！");
            }
            catch (Exception ex)
            {
                MessageBox.Show("设置固定翻译坐标失败！\n原因：" + ex.Message, "截图翻译", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                MinimizeWindow(false);
                if (shot != null && !shot.IsDisposed)
                    shot.Dispose();
            }
        }


        private delegate void SetState(bool minimize);
        // 设置窗口状态，隐藏或显示
        private void MinimizeWindow(bool minimize)
        {
            if (minimize && this.WindowState != FormWindowState.Minimized)
                this.WindowState = FormWindowState.Minimized;
            else if (!minimize && this.WindowState != FormWindowState.Normal)
                this.WindowState = FormWindowState.Normal;
        }


        // 获取窗口句柄，并判断是否有效，无效则抛出异常
        private IntPtr FindWindowHandle()
        {
            IntPtr hwnd;
            if (!string.IsNullOrEmpty(windowName))
            {
                hwnd = Api.FindWindow(null, windowName);
            }
            else if (!string.IsNullOrEmpty(windowClass))
            {
                hwnd = Api.FindWindow(windowClass, null);
            }
            else
                throw new Exception("请设置窗口标题或窗口类名！");

            if (IntPtr.Zero == hwnd)
                throw new Exception("找不到对应的窗口句柄！");
            return hwnd;
        }


        // 截图翻译
        private void ScreenTran()
        {
            string from = "en", src, dst = null;
            Image captureImage;
            FrmScreenShot shot = new FrmScreenShot();
            SetState setState = new SetState(MinimizeWindow);

            try
            {   // 截图翻译并显示
                if (tranMode == TranMode.TranAndShowText)
                {
                    this.Invoke((EventHandler)delegate
                    {
                        setState(true);
                    });
                    Thread.Sleep(200);
                    // 如果是固定区域翻译（isFixedScreen = true则视为固定区域翻译）
                    if (isFixedScreen == true)
                    {
                        if (screenRect.Width <= 0 || screenRect.Height <= 0)
                            throw new Exception("请设置固定截图翻译坐标！");

                        IntPtr hwnd = FindWindowHandle();
                        Api.POINT p = new Api.POINT();
                        p.X = screenRect.X;
                        p.Y = screenRect.Y;
                        Api.ClientToScreen(hwnd, ref p);
                        captureImage = Screenshot(p.X, p.Y, screenRect.Width, screenRect.Height);
                    }
                    else
                    {
                        shot.ScreenImage = shot.CopyScreen();
                        // 显示截图窗口
                        if (shot.ShowDialog() == DialogResult.Cancel)
                            return;
                        captureImage = shot.CaptureImage;
                    }

                    // 翻译模式isEnToZh=true为英译中，false为俄译中
                    if (isEnToZh == false)
                        from = "ru";

                    // sourceOfTran有“有道”两字使用有道翻译，否则使用百度翻译
                    if (sourceOfTran.IndexOf("有道") != -1)
                        Youdao.YoudaoTran(null, from, "zh-CHS", out src, out dst, captureImage);
                    else
                        Baidu.BaiduTran(captureImage, from, out src, out dst);

                    if (copySourceTextToClip)
                        Clipboard.SetText(src);// 复制原文到剪切板
                    if (copyDestTextToClip)
                        Clipboard.SetText(dst);// 复制译文到剪切板
                    if (isSpeak)
                    {
                        SpeechSynthesizer speech = new SpeechSynthesizer();
                        speech.Rate = 3;   // 语速
                        speech.SpeakAsync(dst);
                    }
                }
                else
                {
                    dst = showCont;
                }

                //this.Invoke((EventHandler)delegate
                //{
                //    setState(false);
                //});

                // 不为空
                if (!string.IsNullOrEmpty(dst))
                {
                    using (FrmShowCont sc = new FrmShowCont(showTime))
                    {
                        sc.ContText(dst);
                        sc.ShowDialog();
                    }
                }
            }
            catch (Exception ex)
            {
                if (ex.GetType().FullName == "System.Threading.ThreadAbortException")
                    return;

                //this.Invoke((EventHandler)delegate
                //{
                //    setState(false);
                //});

                dst = "错误：" + ex.Message;
                // 显示错误
                using (FrmShowCont sc = new FrmShowCont(showTime))
                {
                    sc.ContText(dst);
                    sc.ShowDialog();
                }
            }
            finally
            {
                if (shot != null && !shot.IsDisposed)
                    shot.Dispose();
            }
        }


        private void StartThread(string cont)
        {
            // 如果此次按热键的时间距离上次不足300毫秒则忽略掉
            if ((DateTime.Now - lastTime).TotalMilliseconds < 500)
            {
                // lastTime = DateTime.Now;
                return;
            }
            lastTime = DateTime.Now;

            // 显示提示内容
            if (cont != null)
            {   // 如果当前线程未关闭，并且将要关闭的这个线程不为 显示模式（tranMode=TranMode.TranAndShowText）
                if (CloseThread(true) && tranMode != TranMode.ShowText)
                    return;
            }

            // 如果线程正在运行则结束
            if (CloseThread(false))
                Thread.Sleep(100);

            // 启动线程
            newThread = new Thread(ScreenTran);
            newThread.SetApartmentState(ApartmentState.STA);
            newThread.Start();
        }


        /// <summary>
        /// 关闭线程
        /// </summary>
        /// <param name="checkState">此项为true则只返回线程的运行状态</param>
        /// <returns></returns>
        private bool CloseThread(bool checkState)
        {
            if (newThread != null)  // 如果线程还在运行
            {
                if ((newThread.ThreadState & (System.Threading.ThreadState.Stopped | System.Threading.ThreadState.Unstarted)) == 0)
                {
                    if (checkState == false)
                        newThread.Abort(); // 关闭线程
                    return true;
                }
            }
            return false;
        }


        // TranslateForm
        public static int AutoPressKey { get; set; }
        public static int TranDestLang { get; set; }
        public static bool AutoSend { get; set; }

        // 显示翻译窗口
        private void ShowTranForm()
        {
            try
            {
                FrmTranslate ts = new FrmTranslate(sourceOfTran, windowName, windowClass);
                ts.Local = new Point(this.Location.X + this.Width / 2, this.Location.Y + this.Height / 2);
                ts.Show();
            }
            catch
            {
                ;
            }
        }


        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e) => Process.Start("https://ai.baidu.com/tech/ocr/general");
        private void linkLabel2_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e) => Process.Start("https://api.fanyi.baidu.com/");

        private void button_BaiduKeyTest_Click(object sender, EventArgs e)
        {
            if (TextBoxIsEmpty(groupBox1))// 先判断是否有 没有填的项
                return;

            // 先保存数据
            // 百度翻译
            InitDictionary(changeBaiduKey: true);
            if (!Baidu.BaiduKeyTest())
                return;
            try
            {
                ConfigFile.WriteFile(config);
                MessageBox.Show("测试成功！", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("保存到文件失败！\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void button_YoudaoKeyTest_Click(object sender, EventArgs e)
        {
            if (TextBoxIsEmpty(groupBox2))// 先判断是否有 没有填的项
                return;

            // 先保存数据
            // 有道翻译
            InitDictionary(changeYoudaoKey: true);

            if (!Youdao.YoudaoKeyTest())
                return;
            try
            {
                ConfigFile.WriteFile(config);
                MessageBox.Show("测试成功！", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("保存到文件失败！\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private bool TextBoxIsEmpty(Control ctl)
        {
            foreach (var item in ctl.Controls)
            {
                if (item is TextBox)
                {
                    if (string.IsNullOrEmpty(((TextBox)item).Text))
                    {
                        MessageBox.Show("缺少必备的参数！", "失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return true;
                    }
                }
            }
            return false;
        }

        private void button_Reload_Click(object sender, EventArgs e)
        {
            LoadFile(true);
        }

        private void tabControl1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (tabControl1.SelectedIndex == 1)
            {
                UnRegHotKey();
            }
            else if (!putOnTheHook)
                RegHotKey();
        }

        private void textBox_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
                ((TextBox)sender).Text = "";
        }

        private void FrmMain_SizeChanged(object sender, EventArgs e)
        {
            if (this.WindowState == FormWindowState.Minimized)
                this.ShowInTaskbar = false;
            else
                this.ShowInTaskbar = true;
        }

        private void notifyIcon1_Click(object sender, EventArgs e)
        {
            if (this.WindowState == FormWindowState.Minimized)
                this.WindowState = FormWindowState.Normal;
            else
                this.WindowState = FormWindowState.Minimized;
        }

        private void linkLabel3_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e) => Process.Start("https://ai.youdao.com/product-fanyi-picture.s");


        /// <summary>
        /// 从指定坐标截取指定大小区域
        /// </summary>
        /// <param name="x">左上角横坐标</param>
        /// <param name="y">左上角纵坐标</param>
        /// <param name="width">宽度</param>
        /// <param name="height">高度</param>
        /// <returns></returns>
        public static Bitmap Screenshot(int x, int y, int width, int height)
        {
            Bitmap bit = new Bitmap(width, height);
            Graphics g = Graphics.FromImage(bit);

            try
            {
                g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.CopyFromScreen(x, y, 0, 0, new Size(width, height));
                return bit;
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
            finally
            {
                g.Dispose();
                //    bit.Dispose();
            }
        }
    }
}
