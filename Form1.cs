using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;


namespace WarGame
{
    public partial class Form1 : Form
    {
        private static Socket _client;
        private static readonly byte[] Data = new byte[1024];
        private ArrayList _tempFlags = new ArrayList();
        private ArrayList _flags = new ArrayList();
        private String status = "";
        private String state = "FLAG";
        private Dictionary<String, CheckBox> allCities = new Dictionary<string, CheckBox>();

        private Boolean lastResult = false;
        private String lastSendCity;

        public Form1()
        {
            InitializeComponent();
            pictureBox2.SizeMode = PictureBoxSizeMode.StretchImage;
            MaximizeBox = false;
            chooseCountriesButton.Enabled = false;

            prepareAllCities();
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        void prepareAllCities()
        {
            foreach (Control con in this.Controls)
            {
                if (con is CheckBox)
                {
                    CheckBox city = con as CheckBox;
                    allCities.Add(city.Name, city);
                }
            }
        }

        private void CheckedChanged(object sender, EventArgs e)
        {
            var checkBox = sender as CheckBox;
            Dictionary<CheckBox, String> response = getCity(checkBox);

            CheckBox cityCheckbox = null;
            String city = "";
            foreach (var item in response)
            {
                cityCheckbox = item.Key;
                city = item.Value;
            }

            handleCheckboxChange(cityCheckbox, city);
        }



        private void handleCheckboxChange(Control con, String city)
        {

            CheckBox checkbox = con as CheckBox;

            if (checkbox.Checked.Equals(true))
            {
                _tempFlags.Add(city);
            }
            else
            {
                _tempFlags.Remove(city);
            }

            if (state == "FLAG" && _tempFlags.Count > 5)
            {
                showErrorMessage("You can select up to 5 countries.");
            }
            else if (state == "WAR" && _tempFlags.Count > 1)
            {
                showErrorMessage("You can select 1 city per tour.");
            }
            else
            {
                showErrorMessage("");
            }

        }



        private void chooseCountriesButton_Click(object sender, EventArgs e)
        {


            if (!_tempFlags.Count.Equals(5))
            {
                downLabel.Visible = true;
                downLabel.ForeColor = Color.Red;
                downLabel.Text = "You have to select 5 country to continue!";
            }

            else if (_tempFlags.Count.Equals(5))
            {
                downLabel.Visible = true;
                downLabel.Text = "Congratulations! Now you can select a city per tour.";
                chooseCountriesButton.Enabled = false;
                sendButton.Enabled = true;
                state = "WAR";

                foreach (var flag in _tempFlags)
                {
                    _flags.Add(flag);
                }
            }

            if (_flags.Count.Equals(5))
            {

                foreach (Control con in this.Controls)
                {
                    if (con is CheckBox)
                    {
                        CheckBox c = con as CheckBox;
                        c.Checked = false;
                        c.BackColor = Color.MidnightBlue;
                        c.ForeColor = Color.White;
                        c.Text = "Conquer";
                    }
                }
            }

            Console.WriteLine("Size:" + _tempFlags.Count);

            foreach (var c in _tempFlags)
            {
                Console.WriteLine(c);
            }
        }

        private void hostButton_Click(object sender, EventArgs e)
        {
            try
            {
                status = "HOST";
                SetVisibleToTrueAllCheckboxes();
                DisableConnectionButtons();
                topLabel.Text = "Guest player is waiting..";
                downLabel.Visible = true;
                downLabel.Text = "While you wait, you can choose 5 cities for yourself.";
                chooseCountriesButton.Enabled = true;
                Socket newsock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                IPEndPoint iep = new IPEndPoint(IPAddress.Any, 9050);
                newsock.Bind(iep);
                newsock.Listen(5);
                newsock.BeginAccept(new AsyncCallback(AcceptConn), newsock);
            }
            catch (SocketException)
            {
                topLabel.Text = "A error occured. Please restart the game!";
                downLabel.Text = "This port may be in use by the host. Please select GUEST.";
                chooseCountriesButton.Enabled = false;
                SetVisibleToFalseAllCheckboxes();

            }
        }

        private void guestButton_Click(object sender, EventArgs e)
        {
            status = "GUEST";
            SetVisibleToTrueAllCheckboxes();
            DisableConnectionButtons();
            topLabel.Text = "Searching host player..";
            downLabel.Visible = true;
            downLabel.Text = "While you wait, you can choose 5 cities for yourself.";
            chooseCountriesButton.Enabled = true;
            _client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint iep = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 9050);
            _client.BeginConnect(iep, new AsyncCallback(Connected), _client);

        }

        private void sendButton_Click(object sender, EventArgs e)
        {
            if (state == "WAR" && _tempFlags.Count > 1)
            {
                showErrorMessage("You can select 1 region");
            }

            if (_tempFlags.Count == 1)
            {
                lastSendCity = _tempFlags[0].ToString();
                byte[] message = Encoding.ASCII.GetBytes(_tempFlags[0].ToString());
                _client.BeginSend(message, 0, message.Length, 0, new AsyncCallback(SendData), _client);
                sendButton.Enabled = false;
            }

        }

        void SendData(IAsyncResult iar)
        {
            Socket remote = (Socket)iar.AsyncState;
            int sent = remote.EndSend(iar);
        }
        void Connected(IAsyncResult iar)
        {
            try
            {
                _client.EndConnect(iar);
                topLabel.Text = "Host found! IP address: " + _client.RemoteEndPoint.ToString();
                Thread receiver = new Thread(new ThreadStart(ReceiveData));
                receiver.Start();

            }
            catch (SocketException e)
            {
                SetVisibleToFalseAllCheckboxes();
                topLabel.Text = "A problem occured (probably HOST not found!)";
                downLabel.Text = "Please restart the game!";
            }
        }

        void AcceptConn(IAsyncResult iar)
        {
            try
            {
                Socket oldserver = (Socket)iar.AsyncState;
                _client = oldserver.EndAccept(iar);
                topLabel.Text = "Guest found! IP address: " + _client.RemoteEndPoint.ToString();
                Thread receiver = new Thread(new ThreadStart(ReceiveData));
                receiver.Start();
            }
            catch (SocketException e)
            {
                SetVisibleToFalseAllCheckboxes();
                topLabel.Text = "A problem occured. Please restart the game.";
            }

        }


        void ReceiveData()
        {
            int recv;
            string stringData;

            int count = 0;
            while (true)
            {

                recv = _client.Receive(Data);
                stringData = Encoding.ASCII.GetString(Data, 0, recv);


                if (stringData.Equals("fin"))
                {
                    sendButton.Enabled = false;
                    topLabel.ForeColor = Color.Red;
                    topLabel.Text = "YOU LOST THE GAME!";
                    downLabel.Text = "Your opponent has found all your flags!";
                    _client.Close();
                }

                else if (stringData.Equals("true"))
                {
                    count++;
                    String key = "city" + lastSendCity;
                    CheckBox checkBox = allCities[key];
                    checkBox.Checked = false;
                    checkBox.ForeColor = Color.White;
                    checkBox.Text = "captured";
                    checkBox.BackColor = Color.Green;
                    checkBox.Enabled = false;
                    downLabel.ForeColor = Color.Green;
                    downLabel.Text = "Excellent!";
                    result.Text = "Number of flags found: " + count;

                    if (count == 5)
                    {
                        topLabel.ForeColor = Color.Green;
                        topLabel.Text = "YOU WON THE GAME!";
                        downLabel.ForeColor = Color.Green;
                        downLabel.Text = "Excellent! You found all your opponent's flags.";
                        byte[] response = Encoding.ASCII.GetBytes("fin");
                        _client.Send(response);
                        _client.Close();
                    }
                }
                else if (stringData.Equals("false"))
                {
                    String key = "city" + lastSendCity;
                    CheckBox checkBox = allCities[key];
                    checkBox.Checked = false;
                    checkBox.Visible = false;
                    downLabel.Text = "Nice try! But you couldn't find your opponent's flag.";
                }

                else
                {
                    sendButton.Enabled = true;
                    if (_flags.Contains(stringData))
                    {
                        byte[] response = Encoding.ASCII.GetBytes("true");
                        _client.Send(response);
                    }
                    else
                    {
                        byte[] response = Encoding.ASCII.GetBytes("false");
                        _client.Send(response);
                    }
                }



                if (stringData == "bye")
                    break;
            }
            stringData = "bye";
            byte[] message = Encoding.ASCII.GetBytes(stringData);
            _client.Send(message);
            _client.Close();
            return;
        }

        void SetVisibleToTrueAllCheckboxes()
        {
            foreach (Control con in this.Controls)
            {
                if (con is CheckBox)
                {
                    con.Visible = true;
                }
            }
        }

        void SetVisibleToFalseAllCheckboxes()
        {
            foreach (Control con in this.Controls)
            {
                if (con is CheckBox)
                {
                    con.Visible = false;
                }
            }
        }

        void showErrorMessage(String message)
        {
            downLabel.Visible = true;
            downLabel.ForeColor = Color.Red;
            downLabel.Text = message;
        }


        void DisableConnectionButtons()
        {
            guestButton.Enabled = false;
            hostButton.Enabled = false;
        }

        private Dictionary<CheckBox, String> getCity(CheckBox sender)
        {

            if (this.Controls.OfType<CheckBox>().Contains(sender))
            {
                CheckBox city = sender as CheckBox;
                Dictionary<CheckBox, String> response = new Dictionary<CheckBox, string>();
                response.Add(city, city.Name.Split('y')[1].ToString());
                return response;
            }

            return null;
        }

    }
}
