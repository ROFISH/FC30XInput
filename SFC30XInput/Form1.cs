using SharpDX.DirectInput;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using System.Threading;

namespace SFC30XInput
{
    public partial class Form1 : Form
    {
        DirectInput directInput;
        DS4Windows.X360Device x360Bus;

        string runningController;
        Guid runningControllerGuid;
        DeviceType runningControllerType;
        Joystick joystick;
        bool aquired;
        bool xbus_opened;
        bool xinput_plugged;
        JoystickState lastState;
        Thread updateThread;
        byte[] xinputData = new byte[28];

        bool xinput_as_dpad;
        bool xinput_as_360_buttons;

        byte[] Rumble = new byte[8]; //who cares, just compatibility

        public Form1()
        {
            directInput = new DirectInput();
            x360Bus = new DS4Windows.X360Device();
            runningController = null;
            runningControllerGuid = Guid.Empty;
            joystick = null;
            lastState = null;
            updateThread = null;
            aquired = false;
            xbus_opened = false;
            xinput_plugged = false;
            Array.Clear(xinputData, 0, xinputData.Length);

            xinput_as_dpad = false;
            xinput_as_360_buttons = false;

            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            refresh_controllers();
            if (listBox1.Items.Count > 0)
            {
                listBox1.SelectedIndex = 0;
            }
        }

        private void refresh_controllers()
        {
            listBox1.Items.Clear();
            try
            {
                foreach (DeviceInstance deviceInstance in directInput.GetDevices(DeviceType.Gamepad, DeviceEnumerationFlags.AllDevices))
                {
                    listBox1.Items.Add(deviceInstance.ProductName.Replace("\0", ""));
                    //Console.WriteLine(deviceInstance.ProductName.Replace("\0", ""));
                }
                foreach (DeviceInstance deviceInstance in directInput.GetDevices(DeviceType.Joystick, DeviceEnumerationFlags.AllDevices))
                {
                    listBox1.Items.Add(deviceInstance.ProductName.Replace("\0", ""));
                    //Console.WriteLine(deviceInstance.ProductName.Replace("\0", ""));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("{0} {1}", ex.HelpLink, ex.Message);
                //throw;
            }
        }

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            //Console.WriteLine(listBox1.SelectedIndex);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            refresh_controllers();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (runningController == null)
            {
                StartController();
            }
            else
            {
                StopController();
            }
        }

        private bool FindSelectedController()
        {
            if (listBox1.SelectedIndex == -1)
            {
                MessageBox.Show("You need to select a controller silly.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            try
            {
                runningController = listBox1.SelectedItem.ToString();
                //Console.WriteLine(runningController);
            }
            catch (Exception ex)
            {
                Console.WriteLine("{0} {1}", ex.HelpLink, ex.Message);
                //throw;
            }

            foreach (DeviceInstance deviceInstance in directInput.GetDevices(DeviceType.Gamepad, DeviceEnumerationFlags.AllDevices))
            {
                string thisController = deviceInstance.ProductName.Replace("\0", "");
                if (runningController == thisController)
                {
                    runningControllerGuid = deviceInstance.InstanceGuid;
                    runningControllerType = DeviceType.Gamepad;
                    break;
                }
            }
            if (runningControllerGuid == Guid.Empty)
            {
                foreach (DeviceInstance deviceInstance in directInput.GetDevices(DeviceType.Joystick, DeviceEnumerationFlags.AllDevices))
                {
                    string thisController = deviceInstance.ProductName.Replace("\0", "");
                    if (runningController == thisController)
                    {
                        runningControllerGuid = deviceInstance.InstanceGuid;
                        runningControllerType = DeviceType.Joystick;
                        break;
                    }
                }
            }
            if (runningControllerGuid == Guid.Empty)
            {
                MessageBox.Show("Cannot find controller "+runningController+".", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                runningController = null;
                return false;
            }
            return true;
        }

        private void StopController()
        {
            if (aquired)
            {
                joystick.Unacquire();
                aquired = false;
            }
            try
            {
                if (updateThread != null && (updateThread.ThreadState != ThreadState.Stopped || updateThread.ThreadState != ThreadState.Aborted))
                {
                    updateThread.Abort();
                    updateThread.Join();
                    updateThread = null;
                }
            }
            catch//()// (Exception ex)
            {
                //Console.WriteLine("{0} {1}", ex.Message, ex.StackTrace);
            }
            if (joystick != null)
            {
                joystick.Dispose();
                joystick = null;
            }

            if (xinput_plugged)
            {
                x360Bus.Unplug(0);
                xinput_plugged = false;
            }
            if (xbus_opened)
            {
                x360Bus.Stop();
                x360Bus.Close();
            }

            runningController = null;
            runningControllerGuid = Guid.Empty;

            listBox1.Enabled = true;
            groupBox1.Enabled = true;
            groupBox2.Enabled = true;
            button3.Enabled = true;
            button2.Text = "Start Xinput Emulation";
        }

        private void StartController()
        {
            if (!FindSelectedController())
            {
                return;
            }
            try
            {
                joystick = new Joystick(directInput, runningControllerGuid);
                joystick.SetCooperativeLevel(Handle, CooperativeLevel.Exclusive | CooperativeLevel.Background);
                joystick.Properties.AxisMode = DeviceAxisMode.Absolute;
                joystick.Acquire();
                aquired = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("{0} {1}", ex.HelpLink, ex.Message);
                MessageBox.Show("Error trying to Acquire controller exclusively in the background. Perhaps some other app wants it?", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                StopController();
            }


            xinput_as_dpad = radioButton2.Checked;
            xinput_as_360_buttons = radioButton4.Checked;
            
            listBox1.Enabled = false;
            groupBox1.Enabled = false;
            groupBox2.Enabled = false;
            button3.Enabled = false;
            button2.Text = "Stop Xinput Emulation";

            if (x360Bus.Open() && x360Bus.Start())
            {
                xbus_opened = true;
                if (x360Bus.Plugin(0))
                {
                    xinput_plugged = true;
                }
                else
                {
                    MessageBox.Show("Error 'plugging in' xinput controller.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                MessageBox.Show("Error starting 360 bus.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                StopController();
            }

            updateThread = new Thread(UpdateController);
            updateThread.Name = "UpdateController Thread";
            updateThread.Start();
        }

        private void UpdateXinputData()
        {
            xinputData[0] = 0x1C;
            xinputData[4] = (Byte)(1); //plugging in is zero indexed, but update frames are 1 indexed
            xinputData[9] = 0x14;

            for (int i = 10; i < xinputData.Length; i++)
            {
                xinputData[i] = 0;
            }
            /*if (state.Share) xinputData[10] |= (Byte)(1 << 5); // Back
            if (state.L3) xinputData[10] |= (Byte)(1 << 6); // Left  Thumb
            if (state.R3) xinputData[10] |= (Byte)(1 << 7); // Right Thumb
            if (state.Options) xinputData[10] |= (Byte)(1 << 4); // Start

            if (state.DpadUp) xinputData[10] |= (Byte)(1 << 0); // Up
            if (state.DpadRight) xinputData[10] |= (Byte)(1 << 3); // Right
            if (state.DpadDown) xinputData[10] |= (Byte)(1 << 1); // Down
            if (state.DpadLeft) xinputData[10] |= (Byte)(1 << 2); // Left

            if (state.L1) xinputData[11] |= (Byte)(1 << 0); // Left  Shoulder
            if (state.R1) xinputData[11] |= (Byte)(1 << 1); // Right Shoulder

            if (state.Triangle) xinputData[11] |= (Byte)(1 << 7); // Y
            if (state.Circle) xinputData[11] |= (Byte)(1 << 5); // B
            if (state.Cross) xinputData[11] |= (Byte)(1 << 4); // A
            if (state.Square) xinputData[11] |= (Byte)(1 << 6); // X

            if (state.PS) xinputData[11] |= (Byte)(1 << 2); // Guide     

            xinputData[12] = state.L2; // Left Trigger
            xinputData[13] = state.R2; // Right Trigger

            Int32 ThumbLX = Scale(state.LX, false);
            Int32 ThumbLY = -Scale(state.LY, false);
            Int32 ThumbRX = Scale(state.RX, false);
            Int32 ThumbRY = -Scale(state.RY, false);
            xinputData[14] = (Byte)((ThumbLX >> 0) & 0xFF); // LX
            xinputData[15] = (Byte)((ThumbLX >> 8) & 0xFF);
            xinputData[16] = (Byte)((ThumbLY >> 0) & 0xFF); // LY
            xinputData[17] = (Byte)((ThumbLY >> 8) & 0xFF);
            xinputData[18] = (Byte)((ThumbRX >> 0) & 0xFF); // RX
            xinputData[19] = (Byte)((ThumbRX >> 8) & 0xFF);
            xinputData[20] = (Byte)((ThumbRY >> 0) & 0xFF); // RY
            xinputData[21] = (Byte)((ThumbRY >> 8) & 0xFF);*/

            xinputData[14] = 0; //LX
            xinputData[15] = 0; //LX
            xinputData[16] = 0; //LY
            xinputData[17] = 0; //LY
            xinputData[18] = 0; //RX
            xinputData[19] = 0; //RX
            xinputData[20] = 0; //RY
            xinputData[21] = 0; //RY

            // snes controller values
            bool up = false;
            bool down = false;
            bool left = false;
            bool right = false;
            bool a = false;
            bool b = false;
            bool x = false;
            bool y = false;
            bool l = false;
            bool r = false;
            bool select = false;
            bool start = false;

            if (lastState.X < 1000)
            {
                left = true;
            }
            else if (lastState.X > 64535)
            {
                right = true;
            }
            if (lastState.Y < 1000)
            {
                up = true;
            }
            else if (lastState.Y > 64535)
            {
                down = true;
            }
            if (runningControllerType == DeviceType.Gamepad)
            {
                a = lastState.Buttons[0];
                b = lastState.Buttons[1];
                x = lastState.Buttons[3];
                y = lastState.Buttons[4];
                l = lastState.Buttons[6];
                r = lastState.Buttons[7];
                select = lastState.Buttons[10];
                start = lastState.Buttons[11];
            }
            else if (runningControllerType == DeviceType.Joystick)
            {
                a = lastState.Buttons[1];
                b = lastState.Buttons[2];
                x = lastState.Buttons[0];
                y = lastState.Buttons[3];
                l = lastState.Buttons[4];
                r = lastState.Buttons[5];
                select = lastState.Buttons[6];
                start = lastState.Buttons[7];
            }


            if (xinput_as_dpad)
            {
                if (left)
                {
                    xinputData[10] |= (Byte)(1 << 2);
                }
                if (right)
                {
                    xinputData[10] |= (Byte)(1 << 3);
                }
                if (up)
                {
                    xinputData[10] |= (Byte)(1 << 0);
                }
                if (down)
                {
                    xinputData[10] |= (Byte)(1 << 1);
                }
            }
            else
            {
                // top left on 360 (ms original) controller is, not 0,0 or 32767,32767, but 24214;24615; emulate as such
                // 32767 / sqrt(2) = 23105.4211821; so use 23105 as the number to report on both directions; 0x5A41

                if (up && (left || right))
                {
                    xinputData[16] = 0x41; //LY
                    xinputData[17] = 0x5A; //LY
                }
                else if (up)
                {
                    xinputData[16] = 0xFF; //LY
                    xinputData[17] = 0x7F; //LY
                }
                if (down && (left || right))
                {
                    xinputData[16] = 0xBF; //LY
                    xinputData[17] = 0xA5; //LY
                }
                else if (down)
                {
                    xinputData[16] = 0x01; //LY
                    xinputData[17] = 0x80; //LY
                }

                if (right && (up || down))
                {
                    xinputData[14] = 0x41; //LX
                    xinputData[15] = 0x5A; //LX
                }
                else if (right)
                {
                    xinputData[14] = 0xFF; //LX
                    xinputData[15] = 0x7F; //LX
                }
                if (left && (up || down))
                {
                    xinputData[14] = 0xBF; //LX
                    xinputData[15] = 0xA5; //LX
                }
                else if (left)
                {
                    xinputData[14] = 0x01; //LX
                    xinputData[15] = 0x80; //LX
                }
            }

            if (xinput_as_360_buttons)
            {
                if (b) xinputData[11] |= (Byte)(1 << 4); // A
                if (a) xinputData[11] |= (Byte)(1 << 5); // B
                if (y) xinputData[11] |= (Byte)(1 << 6); // X
                if (x) xinputData[11] |= (Byte)(1 << 7); // Y
            }
            else
            {
                if (a) xinputData[11] |= (Byte)(1 << 4); // A
                if (b) xinputData[11] |= (Byte)(1 << 5); // B
                if (x) xinputData[11] |= (Byte)(1 << 6); // X
                if (y) xinputData[11] |= (Byte)(1 << 7); // Y
            }
            if (l) xinputData[11] |= (Byte)(1 << 0); // Left  Shoulder
            if (r) xinputData[11] |= (Byte)(1 << 1); // Right Shoulder
            if (select) xinputData[10] |= (Byte)(1 << 5); // Back
            if (start) xinputData[10] |= (Byte)(1 << 4); // Start
        }

        private void UpdateController()
        {
            while (true)
            {
                try {
                    joystick.Poll();
                    lastState = joystick.GetCurrentState();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("{0} {1} {2}", ex.GetType().ToString(), ex.Message, ex.StackTrace);
                    throw;
                }

                UpdateXinputData();
                x360Bus.Report(xinputData, Rumble);

                //Console.WriteLine(lastState.ToString());
            }
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            try {
                StopController();
            }
            catch (Exception ex)
            {
                Console.WriteLine("{0} {1}", ex.Message, ex.StackTrace);
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            x360Bus.Open();
            x360Bus.Start();
            x360Bus.Unplug(0);
            x360Bus.Stop();
            x360Bus.Close();
            refresh_controllers();
        }
    }
}
