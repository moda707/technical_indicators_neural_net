using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using vtocSqlInterface;
using OfficeOpenXml;
using System.IO;
using System.Threading;

using AForge;
using AForge.Neuro;
using AForge.Neuro.Learning;


using Accord.Neuro;
using Accord.Math;
using Accord.Math.Optimization;
using Accord.Statistics;
using Accord.Neuro.Learning;
using AForge.Controls;
using BaseClasses;

namespace Indicator_Combination
{
    public partial class From1 : Form
    {
        private bool useRegularization;
        private volatile bool needToStop = false;
        private double learningRate = 0.001;
        private double sigmoidAlphaValue = 2.0;
        private int InputLayerDataCount = 41;
        private int predictionSize = 1;
        private int iterations = 500;
        private int iteration = 0;
        private DataTable AllData;
        private List<Symbols> Symbol;
        private List<Symbols> FinalSymbol;
        ActivationNetwork network;
        LevenbergMarquardtLearning teacher;
        private string Scope;
        private string SDEven;
        private string SHEven;
        private string EDEven;
        private string EHEven;
        private string MPeriod;
        private string Step;
        private string smplNumber;
        private double TestTrainRate;
        private vtocSqlInterface.sqlInterface mySql;
        private string sqlCmd;
        private delegate void SetTextCallback(System.Windows.Forms.Control control, string text);

        public From1()
        {
            InitializeComponent();
        }

        private void From1_Load(object sender, EventArgs e)
        {  
            FinalSymbol = new List<Symbols>();
            cmbScop.SelectedItem = cmbScop.Items[0];
            Symbol = ReadSymbols("All").OrderBy(t => t.Symbol).ToList();
            lstFirstSymbol.DataSource = Symbol;
            lstFirstSymbol.ValueMember = "InsCode";
            lstFirstSymbol.DisplayMember = "Symbol";

            startButton.Enabled = true;

            chart1.ChartAreas[0].AxisY.ScaleView.Zoomable = true;
            chart1.ChartAreas[0].AxisX.ScaleView.Zoomable = true;

            chart2.ChartAreas[0].AxisY.ScaleView.Zoomable = true;
            chart2.ChartAreas[0].AxisX.ScaleView.Zoomable = true;

            chart3.ChartAreas[0].AxisY.ScaleView.Zoomable = true;
            chart3.ChartAreas[0].AxisX.ScaleView.Zoomable = true;
        }       

        private void txtsymbolsearch_TextChanged(object sender, EventArgs e)
        {
            List<Symbols> tmpSymbol = new List<Symbols>();

            tmpSymbol = Symbol.FindAll(t => t.Symbol.Contains(txtsymbolsearch.Text));
            lstFirstSymbol.DataSource = tmpSymbol;
            lstFirstSymbol.ValueMember = "InsCode";
            lstFirstSymbol.DisplayMember = "Symbol";
            lstFirstSymbol.SelectedValue = "Selected";
        }

        private List<Symbols> ReadSymbols(string type)
        {
            List<Symbols> tmpSymb = new List<Symbols>();
            tmpSymb = new List<Symbols>();

            mySql = new sqlInterface(Properties.Settings.Default.sqlserver, "AdjPrice",
                                     Properties.Settings.Default.username, Properties.Settings.Default.pass);
            DataTable dtSymbols;
            switch (type)
            {
                case "All":
                    sqlCmd = @"  SELECT DISTINCT S.LVal18AFC as Symbol, S.InsCode as InsCode
                          FROM [TseTrade].[dbo].[vwTseInstrument] S
                          JOIN TseTrade.dbo.vwTsePrice T ON T.InsCode = S.InsCode
                          WHERE S.Flow in (1,2) and YMarNSC='No' and YVal in (300 ,303)
                          ORDER BY LVal18AFC";
                    dtSymbols = mySql.SqlExecuteReader(sqlCmd);

                    foreach (DataRow row in dtSymbols.Rows)
                    {
                        if (dtSymbols.Columns.Contains("InsCode") && dtSymbols.Columns.Contains("Symbol"))
                        {
                            var sCode = row["InsCode"].ToString();
                            var sName = row["Symbol"].ToString();

                            tmpSymb.Add(new Symbols(sName, sCode));
                        }
                    }
                    break;
                case "Top50":
                    sqlCmd = "DECLARE @SDEven int = " + txtSDEven.Text + " DECLARE @EDEven int = " + txtDEven.Text;
                    sqlCmd += @" DECLARE @TTABLE Table(InsCode bigint, Volume bigint, CSecVal int)  
                            INSERT INTO @TTABLE
                            SELECT T.InsCode, SUM(T.QTotTran5J) Volume, MAX(S.CSecVal) CSecVal
                            FROM TseTrade.dbo.TsePrice T
                            JOIN TseTrade.dbo.TseInstrument S ON S.InsCode = T.InsCode
                            WHERE T.IsLastRecordDaily = 1 AND (T.DEven between @SDEven and @EDEven) AND S.YMarNSC='NO' AND S.Flow=1 AND S.YVal <> 400
                            GROUP BY T.InsCode
                            SELECT TOP 50 S.LVal18AFC Symbol, S.InsCode InsCode
                            FROM  @TTABLE T
                            JOIN TseTrade.dbo.vwTseInstrument S ON S.InsCode = T.InsCode
                            JOIN TseTrade.dbo.TseSector W ON W.CSecVal = S.CSecVal
                            ORDER BY  Volume DESC";
                    dtSymbols = mySql.SqlExecuteReader(sqlCmd);

                    foreach (DataRow row in dtSymbols.Rows)
                    {
                        if (dtSymbols.Columns.Contains("InsCode") && dtSymbols.Columns.Contains("Symbol"))
                        {
                            var sCode = row["InsCode"].ToString();
                            var sName = row["Symbol"].ToString();

                            tmpSymb.Add(new Symbols(sName, sCode));
                        }
                    }
                    break;
            }

            return tmpSymb;
        }

        private void btnoneright_Click(object sender, EventArgs e)
        {
            if (!FinalSymbol.Contains((Symbols)lstFirstSymbol.SelectedItem))
            {
                lstFinalSymbol.Items.Add((Symbols)lstFirstSymbol.SelectedItem);
                lstFinalSymbol.ValueMember = "InsCode";
                lstFinalSymbol.DisplayMember = "Symbol";
                lstFinalSymbol.SelectedIndex = lstFinalSymbol.Items.Count - 1;

                FinalSymbol.Add((Symbols)lstFirstSymbol.SelectedItem);
                SymbolCounter();
            }
        }

        private void btnoneleft_Click(object sender, EventArgs e)
        {
            FinalSymbol.Remove((Symbols)lstFinalSymbol.SelectedItem);
            lstFinalSymbol.Items.Remove((Symbols)lstFinalSymbol.SelectedItem);
            SymbolCounter();
        }

        private void SymbolCounter()
        {
            txtSymCount.Text = FinalSymbol.Count.ToString();
        }

        private void btnOptimize_Click(object sender, EventArgs e)
        {
            startButton.Enabled = false;
            stopButton.Enabled = true;
            needToStop = false;
            Scope = cmbScop.Text;
            SDEven = txtSDEven.Text;
            SHEven = txtSHEven.Text;
            EDEven = txtDEven.Text;
            EHEven = txtHEven.Text;
            MPeriod = txtMPeriod.Text;
            Step = txtstep.Text;
            TestTrainRate = Convert.ToDouble(txttesttrainrate.Text);
            
            backgroundWorker1.RunWorkerAsync();
        }


        private Neural_Network NueralNetworkOptimization(Symbols a)
        {
            //Initialize
            Neural_Network NNObj = new Neural_Network();
            double TestError = 0;
            double TrainError = 0;

            mySql = new sqlInterface(Properties.Settings.Default.sqlserver, "TseTrade",
                                      Properties.Settings.Default.username, Properties.Settings.Default.pass);


           // sqlCmd = string.Format(@"DECLARE @InsCode bigint = {0} DECLARE @SDEven int = {1} DECLARE @SHEven int = {2} DECLARE @EDEven int = {3} DECLARE @EHEven int = {4} DECLARE @MPeriod int = {5}  ", a.InsCode, SDEven, SHEven, EDEven, EHEven, MPeriod);
           // sqlCmd += System.IO.File.ReadAllText("Query1.txt");
            sqlCmd = string.Format(@"SELECT *  FROM AdjPrice.dbo.TempPriceData T WHERE T.InsCode = {0}  AND ((T.DEven={1} AND T.HEven>{2}) OR (T.DEven>{1})) AND ((T.DEven={3} AND T.EHven<{4})OR (T.DEven<{3}))  ORDER BY T.DEven, T.HEven", a.InsCode, SDEven, SHEven, EDEven, EHEven);

            AllData = new DataTable();
            AllData = mySql.SqlExecuteReader(sqlCmd);

            SetText(txtcrntSymb, a.Symbol.ToString());

            //stopButton.Enabled = true;
            // run worker thread
            needToStop = false;
            // create multi-layer neural network
            network = new ActivationNetwork(
                new BipolarSigmoidFunction(2),
                InputLayerDataCount, InputLayerDataCount * 2, 1);
            
            try
            {
                // initialize input and output values 
                int SamplesCountTrain = 0;
                int SamplesCountTest = 0;

                double[][] input0 = new double[0][];
                double[][] output0 = new double[0][];
                double[][] input1 = new double[0][];
                double[][] output1 = new double[0][];
                double[] price0 = new double[0];
                double[] price1 = new double[0];




                SamplesCountTrain = Convert.ToInt32(AllData.Rows.Count * TestTrainRate);
                SamplesCountTest = Convert.ToInt32(AllData.Rows.Count - SamplesCountTrain);

                InOutData[] myData = new InOutData[AllData.Rows.Count];
                InOutData[] myRawData = new InOutData[AllData.Rows.Count];

                int DataNumberInd = 0;
                FIFO[] FList = new FIFO[InputLayerDataCount];

                for (int j = 0; j < InputLayerDataCount; j++)
                {
                    FList[j] = new FIFO(5);
                    
                }

                foreach (DataRow w in AllData.Rows)
                {
                    InOutData tmpdata = new InOutData();
                    double[] tmpinp = new double[InputLayerDataCount];
                    int j = 0;
                    for (j = 0; j < 21; j++)
                    {
                        if (w[5 + j].ToString() != "")
                            tmpinp[j] = Convert.ToDouble(w[5 + j]);
                    }


                    for (j = 21; j < InputLayerDataCount; j++)
                    {
                        F_Item NN = new F_Item();
                        NN.DEven = w["DEven"].ToString();
                        NN.HEven = w["HEven"].ToString();
                        NN.NNO = 0;
                        if (w[5 + j].ToString() != "")
                            NN.NNO = Convert.ToDouble(w[5 + j]);
                        FList[j].Push(NN);
                        tmpinp[j] = FList[j].GetMeanValue();
                    }

                    tmpdata.InputD = tmpinp;

                    double[] tmpoutp = new double[1];
                    //for (int j = 0; j < 2; j++)
                    //{
                    //    tmpoutp[j] = Convert.ToDouble(w[6 + j]);
                    //}


                    tmpoutp[0] = Convert.ToDouble(w[4]);
                    tmpdata.OutputD = tmpoutp;

                    tmpdata.price = Convert.ToDouble(w[3]);

                    myData[DataNumberInd] = tmpdata;
                    DataNumberInd++;
                }

                myRawData = myData;

                //Shuffle the Array
                myData = Shuffle<InOutData>(myData);


                SetText(txtsmpl, SamplesCountTrain.ToString());

                input0 = new double[SamplesCountTrain][];
                input1 = new double[SamplesCountTest][];

                output0 = new double[SamplesCountTrain][];
                output1 = new double[SamplesCountTest][];

                price0 = new double[SamplesCountTrain];
                price1 = new double[SamplesCountTest];

                int k = 0;
                for (int i = 0; i < SamplesCountTrain; i++)
                {
                    input0[i] = myData[k].InputD;
                    output0[i] = myData[k].OutputD;
                    price0[i] = myData[k].price;

                    k++;
                }
                for (int i = 0; i < SamplesCountTest; i++)
                {
                    input1[i] = myData[k].InputD;
                    output1[i] = myData[k].OutputD;
                    price1[i] = myData[k].price;

                    k++;
                }



                // create teacher
                teacher = new LevenbergMarquardtLearning(network, useRegularization);

                
                // set learning rate
                teacher.LearningRate = learningRate;
                
                // loop 
                int notchanged = 0;
                double LastErr = 0;

                iteration = 0;
                FIFO TrainTestGapQ = new FIFO(5);                


                while (!needToStop && notchanged <= 20 && iteration<500)
                {

                    // run epoch of learning procedure 
                    // Train
                    TrainError = teacher.RunEpoch(input0, output0) / SamplesCountTrain;
                               


                    // Test
                    TestError = 0;
                    double[][] TestOutput = new double[SamplesCountTest][];
                    for (int i = 0; i < input1.Count(); i++)
                    {
                        TestOutput[i] = network.Compute(input1[i]);
                        TestError += Math.Pow((TestOutput[i][0] - output1[i][0]), 2);// +Math.Pow((TestOutput[i][1] - output1[i][1]), 2);
                    }

                    TestError /= SamplesCountTest;// *2;
                    
                    F_Item ErrI = new F_Item();                    
                    ErrI.NNO = TestError - TrainError;
                    TrainTestGapQ.Push(ErrI);

                    if (Math.Abs(TrainError - LastErr) < 0.00001)
                    {
                        notchanged++;
                    }
                    else
                    {
                        notchanged = 0;
                    }
                    LastErr = TrainError;

                    
                    var d = new double[input0.Count()][];

                    for (int i = 0; i < input0.Count(); i++)
                    {
                        d[i] = network.Compute(myRawData[i].InputD);
                    }

                    if (iteration > 4 && (TrainTestGapQ.GetMeanDiff() > 0.01) && TrainError<0.1)
                    {
                        needToStop = true;
                    }

                    backgroundWorker1.ReportProgress(iteration, new Result(myRawData, d, TrainError, TestError, iteration, (int)(iteration / 500.0)));

                    // set current iteration's info
                    SetText(currentIterationBox, iteration.ToString());
                    SetText(currentLearningErrorBox, TrainError.ToString());
                    SetText(currentPredictionErrorBox, TestError.ToString());

                    // increase current iteration
                    iteration++;
                }
            }
            catch (Exception e)
            {
                ;
            }


            //Neural Network Object
            NNObj.Network = network;
            NNObj.Symbol = a;
            NNObj.Periodicity = Convert.ToInt16(Scope);
            NNObj.MPeriod = Convert.ToInt16(MPeriod);
            NNObj.SDEven = SDEven;
            NNObj.SHEven = SHEven;
            NNObj.EDEven = EDEven;
            NNObj.EHEven = EHEven;
            NNObj.TestError = TestError;
            NNObj.TrainError = TrainError;

            return NNObj;
        }

        private void SetText(System.Windows.Forms.Control control, string text)
        {
            if (control.InvokeRequired)
            {
                SetTextCallback d = new SetTextCallback(SetText);
                Invoke(d, new object[] { control, text });
            }
            else
            {
                control.Text = text;
            }
        }

        private void stopButton_Click(object sender, System.EventArgs e)
        {

            // stop worker thread
            needToStop = true;

            backgroundWorker1.CancelAsync();
            
        }

        private void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {
            DoJob();
        }

        private void DoJob()
        {
            foreach (var a in FinalSymbol)
            {
                if (chkchart.Checked)
                {
                    //#region Chart
                    //int cc = 0;
                    //if (chart1.Series.Count > 0 && chart1.Series[cc].Points.Count > 0)
                    //    chart1.Series[cc].Points.Clear();
                    //cc = 0;
                    //if (chart2.Series.Count > 0 && chart2.Series[cc].Points.Count > 0)
                    //    chart2.Series[cc].Points.Clear();
                    //cc = 1;
                    //if (chart2.Series.Count > 0 && chart2.Series[cc].Points.Count > 0)
                    //    chart2.Series[cc].Points.Clear();
                    //cc = 2;
                    //if (chart2.Series.Count > 0 && chart2.Series[cc].Points.Count > 0)
                    //    chart2.Series[cc].Points.Clear();
                    //#endregion
                }

                //ActivationNetwork resultnetwork = new ActivationNetwork(new BipolarSigmoidFunction(2), InputLayerDataCount, InputLayerDataCount * 4, 1);
                Neural_Network NNObj = new Neural_Network();
                NNObj = NueralNetworkOptimization(a);                
                string filename = txtdespath.Text + "\\" + a.InsCode;
                try
                {
                    //NNObj.SerializeMe(filename);
                    Serializer.SerializeNeuralNetwork(NNObj, filename);
                }
                catch (Exception e)
                {
                    ;
                }
                

            }
        }

        private void backgroundWorker1_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            Application.DoEvents();
            var r = (Result)e.UserState;
            if (chkchart.Checked)
            {
                #region Chart


                double x = (double)r.itterationCount;
                double y = (double)r.error;
                double yTest = (double)r.TestErr;


                double[][] y2a = r.Output_desired;
                double[][] y2b = r.Output_actual;


                chart1.Series[0].Points.AddXY(x, y);
                chart1.Series[1].Points.AddXY(x, yTest);

                chart2.Series[0].Points.Clear();
                chart2.Series[1].Points.Clear();
                chart2.Series[2].Points.Clear();

                chart3.Series[0].Points.Clear();
                chart3.Series[1].Points.Clear();
                chart3.Series[2].Points.Clear();


                foreach (var item in y2a)
                {
                    chart2.Series[0].Points.AddY(item[0]);
                    //chart3.Series[0].Points.AddY(item[1]);
                }
                foreach (var item in y2b)
                {
                    chart2.Series[1].Points.AddY(item[0]);
                    //chart3.Series[1].Points.AddY(item[1]);
                }

                foreach (var item in r.price)
                {
                    chart2.Series[2].Points.AddY(item);
                    chart3.Series[2].Points.AddY(item);
                }

                chart1.Invalidate();
                chart2.Invalidate();
                chart3.Invalidate();
                #endregion
            }
        }

        private void backgroundWorker1_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            startButton.Enabled = true;

        }

        private void lstFirstSymbol_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            btnoneright_Click(sender, e);
        }

        private void lstFinalSymbol_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            btnoneleft_Click(sender, e);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            //saveFileDialog1.ShowDialog();
            folderBrowserDialog1.ShowDialog();
            if (folderBrowserDialog1.SelectedPath.ToString() != "")
            {
                if (network != null)
                {
                    string a = folderBrowserDialog1.SelectedPath + "\\" + txtSDEven.Text + "_" + txtDEven.Text + "_" + txtcrntSymb.Text + "_" + cmbScop.Text + "_" + txtsmpl.Text + "_" + iteration.ToString();
                    network.Save(a);
                }
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            openFileDialog1.ShowDialog();
            string txtfile = "";
            if (openFileDialog1.FileName != "")
            {
                txtfile = System.IO.File.ReadAllText(openFileDialog1.FileName);
                //lstFinalSymbol.Items.Clear();
                //FinalSymbol.Clear();

                string[] tmptxt1 = txtfile.Split('$');
                foreach (var a in tmptxt1)
                {
                    string[] tmptxt2 = a.Split('!');
                    Symbols tmpsymb = new Symbols(tmptxt2[0], tmptxt2[1]);
                    if (!FinalSymbol.Contains(tmpsymb))
                    {
                        lstFinalSymbol.Items.Add(tmpsymb);
                        lstFinalSymbol.ValueMember = "InsCode";
                        lstFinalSymbol.DisplayMember = "Symbol";
                        lstFinalSymbol.SelectedIndex = lstFinalSymbol.Items.Count - 1;
                        FinalSymbol.Add(tmpsymb);
                    }
                }
            }
            SymbolCounter();
        }

        private void button3_Click(object sender, EventArgs e)
        {
            string txtfile = "";
            saveFileDialog1.ShowDialog();
            if (saveFileDialog1.FileName != "")
            {
                foreach (Symbols a in FinalSymbol)
                {
                    txtfile += a.Symbol + "!" + a.InsCode + "$";
                }
                txtfile = txtfile.Substring(0, txtfile.Count() - 1);

                System.IO.File.WriteAllText(saveFileDialog1.FileName, txtfile);

            }
        }

        

        public T[] Shuffle<T>(T[] array)
        {
            var random = new Random();
            for (int i = array.Length; i > 1; i--)
            {
                // Pick random element to swap.
                int j = random.Next(i); // 0 <= j <= i-1
                // Swap.
                T tmp = array[j];
                array[j] = array[i - 1];
                array[i - 1] = tmp;
            }
            return array;
        }
                
        private void chkall_CheckedChanged(object sender, EventArgs e)
        {
            Symbol = new List<Symbols>();
            if (chkall.Checked)
            {
                if (lstFirstSymbol.Items.Count > 0)
                    lstFirstSymbol.DataSource = null;
                Symbol = ReadSymbols("All").OrderBy(t => t.Symbol).ToList();
                lstFirstSymbol.DataSource = Symbol;
                lstFirstSymbol.ValueMember = "InsCode";
                lstFirstSymbol.DisplayMember = "Symbol";
            }
        }

        private void chktop50_CheckedChanged(object sender, EventArgs e)
        {
            Symbol = new List<Symbols>();
            if (chktop50.Checked)
            {
                if (lstFirstSymbol.Items.Count > 0)
                    lstFirstSymbol.DataSource = null;
                lstFirstSymbol.Enabled = false;
                Application.DoEvents();
                Symbol = ReadSymbols("Top50").OrderBy(t => t.Symbol).ToList();
                lstFirstSymbol.DataSource = Symbol;
                lstFirstSymbol.ValueMember = "InsCode";
                lstFirstSymbol.DisplayMember = "Symbol";
                lstFirstSymbol.Enabled = true;
            }
        }

        private void btnrightall_Click(object sender, EventArgs e)
        {            
            foreach (var l in Symbol)
            {
                if (!FinalSymbol.Contains(l))
                {
                    lstFinalSymbol.Items.Add(l);
                    lstFinalSymbol.ValueMember = "InsCode";
                    lstFinalSymbol.DisplayMember = "Symbol";
                    FinalSymbol.Add(l);
                }
            }
            SymbolCounter();
        }

        private void btnleftall_Click(object sender, EventArgs e)
        {
            FinalSymbol.Clear();
            lstFinalSymbol.Items.Clear();
            SymbolCounter();
        }

        
    }

    public class InOutData
    {
        public double[] InputD;
        public double[] OutputD;
        public double price;
    }

    public class Result
    {
        public double[][] Output_desired;
        public double[][] Output_actual;
        public double error;
        public double TestErr;
        public double[] price;
        public int itterationCount;
        public int progressPrecent;

        public Result(InOutData[] myData, double[][] output_a, double err, double errT, int ittCnt, int prg)
        {
            Output_desired = new double[myData.Count()][];
            price = new double[myData.Count()];
            for(int i=0; i< myData.Count(); i++){
                Output_desired[i] = myData[i].OutputD;
                price[i] = myData[i].price;
            }
            
            Output_actual = output_a;
            error = err;
            TestErr = errT;
            itterationCount = ittCnt;
            progressPrecent = prg;            
            
        }
    }
        
    
}
