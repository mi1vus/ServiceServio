using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using ServioPumpGAS_Driver;

namespace Service
{
    public partial class Form1 : Form
    {
        [XmlRoot("terminaldictionary")]
        public class Dictionary
        {
            [XmlElement("operators")]
            public List<HandBookRow> Operators { get; set; }
            [XmlElement("azses")]
            public List<AZS> AZSes { get; set; }
            [XmlElement("terminals")]
            public List<Terminal> Terminals { get; set; }
        }
        public class HandBookRow
        {
            [XmlElement("id")]
            public int Id { get; set; }
            [XmlElement("name")]
            public string Name { get; set; }
            public override string ToString()
            {
                return Name;
            }
        }
        public class AZS
        {
            [XmlElement("id")]
            public int Id { get; set; }
            [XmlElement("name")]
            public string Name { get; set; }
            [XmlElement("idoperator")]
            public int IdOperator { get; set; }
        }
        public class Terminal
        {
            [XmlElement("id")]
            public int Id { get; set; }
            [XmlElement("ip")]
            public string Ip { get; set; }
            [XmlElement("name")]
            public string Name { get; set; }
            [XmlElement("idazs")]
            public int IdAZS { get; set; }

            public Driver.TermInfo Info;
        }

        private static Dictionary dictionary;

        private static bool isValidXml(string candidate)
        {
            try
            {
                XElement.Parse(candidate);
            }
            catch (XmlException) { return false; }
            return true;
        }

        private static void ReadXml()
        {
            XmlSerializer serializer = new XmlSerializer(typeof(Dictionary));
            var settFile = File.ReadAllText("Terminals.xml");
            if (!isValidXml(settFile))
                return;

            using (TextReader reader = new StringReader(settFile))
            {
                dictionary = (Dictionary) serializer.Deserialize(reader);
            }
        }

        private static List<Терминал> FormListOfTerminals(int? operatorId = -1, int? azsId = -1)
        {
            if (dictionary == null)
                ReadXml();

            var azses = dictionary.Operators.Join(
                dictionary.AZSes,
                oper => oper.Id,
                azs => azs.IdOperator,
                (oper, azs) => new
                {
                    azsid = azs.Id,
                    azs = azs.Name,
                    oper = oper.Name,
                    operId = oper.Id
                }
            );
            var r1 = azses.ToList();

            var terminals = r1.Join(
                dictionary.Terminals,
                azs => azs.azsid,
                term => term.IdAZS,
                (azs, term) => new Терминал(
                        term.Id,
                        azs.azs,
                        azs.azsid,
                        term.Name,
                        azs.oper,
                        azs.operId,
                        term.Ip,
                        term.Info.State == Driver.TermInfo.States.Work ? "" : term.Info.State.ToString()
                    )
            );

            if (operatorId > 0)
                terminals = terminals.Where(t => t.ОператорId == operatorId);

            if (azsId > 0)
                terminals = terminals.Where(t => t.АЗСId == azsId);

            return terminals.ToList();
        }

        private void ColorizeRow(int index, string info)
        {
            dataGridView1.Rows[index].DefaultCellStyle.BackColor =
                info == "Error" ? Color.PaleVioletRed :
                info == "Warning" ? Color.Orange :
                Color.White;
        }

        class Терминал
        {
            public int Id;
            public string АЗС { get; set; }
            public string Название { get; set; }
            public int АЗСId; 
            public string Оператор;
            public int ОператорId;
            public string IP;
            public string Статус { get; set; }

            public Терминал(int id, string азс, int азсId, string название, string оператор, int операторId, string ip, string статус)
            {
                this.Id = id;
                this.АЗС = азс;
                this.АЗСId = азсId;
                this.Название = название;
                this.Оператор = оператор;
                this.ОператорId = операторId;
                this.IP = ip;
                this.Статус = статус;
            }

            public override string ToString()
            {
                return $"[{IP}] {Название} - {Оператор}";
            }
        }

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            BindingList<Терминал> data = new BindingList<Терминал>(); //Специальный список List с вызовом события обновления внутреннего состояния, необходимого для автообновления datagridview
            dataGridView1.DataSource = data;

            //ServioPumpGAS_Driver.Driver.Service(new Dictionary<string, string>());
            var xmlObj = FormListOfTerminals();
            var operators = xmlObj.GroupBy(t => t.ОператорId)
                .Select(t => new HandBookRow { Id = t.First().ОператорId, Name = t.First().Оператор })
                .ToList();
            comboBox1.Items.Add(new HandBookRow() { Id = -1, Name = "Все" });
            comboBox1.Items.AddRange(operators.ToArray());

            var azses = xmlObj.GroupBy(t => t.АЗСId)
                .Select(t => new HandBookRow { Id = t.First().АЗСId, Name = t.First().АЗС })
                .ToList();
            comboBox2.Items.Add(new HandBookRow() { Id = -1, Name = "Все" });
            comboBox2.Items.AddRange(azses.ToArray());
            comboBox2.SelectedIndex = 0;
            comboBox1.SelectedIndex = 0;

            Driver.TerminalStateChanged += TerminalStateChanged;
            Driver.StartWatch(xmlObj.Select(t => new Driver.TermInfo() { IP = t.IP }).ToArray());
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Driver.Service(
                (dataGridView1.DataSource as BindingList<Терминал>)
                .Select(t => new { t.Id, t.IP })
                .ToDictionary(i => i.Id.ToString(), t => t.IP)
                );
        }

        private void ComboBox_SelectChanged(object sender, EventArgs e)
        {
            var operId = (comboBox1.SelectedItem as HandBookRow)?.Id;
            var azsId = (comboBox2.SelectedItem as HandBookRow)?.Id;

            var data = dataGridView1.DataSource as BindingList<Терминал>;
            data.Clear();
            int index = 0;
            foreach (var terminal in FormListOfTerminals(operId, azsId))
            {
                (dataGridView1.DataSource as BindingList<Терминал>).Add(terminal);
                ColorizeRow(index, terminal.Статус);
                ++index;
            }
        }

        private void TerminalStateChanged(object sender, Driver.TerminalStateChangedEventArgs args)
        {
            lock (dictionary)
            {
                foreach (var row in dictionary.Terminals)
                {
                    if (row.Ip.CompareTo(args.Info.IP) == 0)
                    {
                        row.Info = args.Info;
                        int index = 0;
                        foreach (var tableRow in dataGridView1.DataSource as BindingList<Терминал>)
                        {
                            if (tableRow.IP.CompareTo(args.Info.IP) == 0)
                            {
                                tableRow.Статус = args.Info.State == Driver.TermInfo.States.Work
                                    ? ""
                                    : args.Info.State.ToString();
                                ColorizeRow(index, tableRow.Статус);
                                break;
                            }
                            ++index;
                        }
                        break;
                    }
                }
            }
        }

        static Driver.TermInfo[] terminals;
    }
}
