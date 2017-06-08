using System;
using System.Collections.Generic;
using System.ComponentModel;
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
            public List<Operator> Operators { get; set; }
            [XmlElement("azses")]
            public List<AZS> AZSes { get; set; }
            [XmlElement("terminals")]
            public List<Terminal> Terminals { get; set; }
        }
        public class Operator
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
        }

        private static bool isValidXml(string candidate)
        {
            try
            {
                XElement.Parse(candidate);
            }
            catch (XmlException) { return false; }
            return true;
        }

        private static List<Терминал> ReadXml(int operatorId = -1)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(Dictionary));
            var settFile = File.ReadAllText("Terminals.xml");
            if (!isValidXml(settFile))
                return new List<Терминал>();

            using (TextReader reader = new StringReader(settFile))
            {
                Dictionary result = (Dictionary)serializer.Deserialize(reader);

                var azses = result.Operators.Join(
                    result.AZSes,
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
                    result.Terminals,
                    azs => azs.azsid,
                    term => term.IdAZS,
                    (azs, term) => new Терминал(
                            term.Id,
                            azs.azs,
                            term.Name,
                            azs.oper,
                            azs.operId,
                            term.Ip
                        )
                );
                if (operatorId <= 0)
                    return terminals.ToList();
                else
                    return terminals.Where(t=>t.ОператорId == operatorId).ToList();
            }

        }

        class Терминал
        {
            public int Id;//Данное свойство не будет отображаться как колонка
            public string Название { get; set; } //обязательно нужно использовать get конструкцию
            public string АЗС { get; set; }
            public string Оператор { get; set; }
            public int ОператорId; //Данное свойство не будет отображаться как колонка
            public string IP { get; set; }

            public Терминал(int id, string азс, string название, string оператор, int операторId, string ip)
            {
                this.Id = id;
                this.АЗС = азс;
                this.Название = название;
                this.Оператор = оператор;
                this.ОператорId = операторId;
                this.IP = ip;
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

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            var data = dataGridView1.DataSource as BindingList<Терминал>;
            data.Clear();
            foreach (var terminal in ReadXml((comboBox1.SelectedItem as Operator).Id))
                (dataGridView1.DataSource as BindingList<Терминал>).Add(terminal);
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            BindingList<Терминал> data = new BindingList<Терминал>(); //Специальный список List с вызовом события обновления внутреннего состояния, необходимого для автообновления datagridview
            dataGridView1.DataSource = data;

            //ServioPumpGAS_Driver.Driver.Service(new Dictionary<string, string>());
            var xmlObj = ReadXml();
            var operators = xmlObj.GroupBy(t => t.ОператорId)
                .Select(t => new Operator { Id = t.First().ОператорId, Name = t.First().Оператор })
                .ToList();
            comboBox1.Items.Add(new Operator() { Id = -1, Name = "Все" });
            comboBox1.Items.AddRange(operators.ToArray());
            comboBox1.SelectedIndex = 0;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            ServioPumpGAS_Driver.Driver.Service(ReadXml((comboBox1.SelectedItem as Operator).Id)
                .Select(t => new { t.Id, t.IP }).ToDictionary(i => i.Id.ToString(), t=>t.IP));
        }
    }
}
