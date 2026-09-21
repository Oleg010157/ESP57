using System;
using System.Drawing;
using System.Windows.Forms;
using ZedGraph;

namespace ICDIBasic
{
    public partial class Form3 : Form
    {
        #region Конструкторы

        private float _in1;
        public float PassIn1
        {
            get { return _in1; }
            set { _in1 = value; }
        }

        public float _in2;
        public float PassIn2
        {
            get { return _in2; }
            set { _in2 = value; }
        }

        private float _in3;
        public float PassIn3
        {
            get { return _in3; }
            set { _in3 = value; }
        }

        private int _ref;
        public int PassRefresh
        {
            get { return _ref; }
            set { _ref = value; }
        }


        public Form1 Frm3Form1;  // адресc на объект типа Form1

        bool _stpStr;
        #endregion

        public Form3()
        {

            _data = new RollingPointPairList(_capacity);
            _data2 = new RollingPointPairList(_capacity);
            _data3 = new RollingPointPairList(_capacity);
            InitializeComponent();

            PrepareGraph();
            zedGraph2.ZoomEvent += new ZedGraphControl.ZoomEventHandler(zedGraph2_ZoomEvent);


            numericUpDown4.Value = timer1.Interval;
            numericUpDown5.Value = (decimal)_step;

            // Всплывающие подсказки
            ToolTip t = new ToolTip();
            t.SetToolTip(numericUpDown4, " Скорость обновления графика в миллисекундах (Мин = 15.6 мс)");//15.6 мс - самое быстрое, что может таймер (ограничение из-за винды)
            t.SetToolTip(numericUpDown5, " Шаг, с которым будет обновляться график ");
        }


        #region PrepareGraph
        public void PrepareGraph()
        {
            // Получим панель для рисования
            GraphPane pane = zedGraph2.GraphPane;

            // Очистим список кривых на тот случай, если до этого сигналы уже были нарисованы
            pane.CurveList.Clear();

            // Закрасим фон всего компонента ZedGraph
            // Заливка будет сплошная
            pane.Fill.Type = FillType.Solid;
            pane.Fill.Color = Color.Silver;

            // Закрасим область графика (его фон) в черный цвет
            pane.Chart.Fill.Type = FillType.Solid;
            pane.Chart.Fill.Color = Color.Black;

            // Включим показ оси на уровне X = 0 и Y = 0, чтобы видеть цвет осей
            pane.XAxis.MajorGrid.IsZeroLine = true;
            pane.YAxis.MajorGrid.IsZeroLine = true;
            // Установим цвет осей
            pane.XAxis.Color = Color.Gray;
            pane.YAxis.Color = Color.Gray;

            // Включим сетку
            pane.XAxis.MajorGrid.IsVisible = true;
            pane.YAxis.MajorGrid.IsVisible = true;
            // Установим цвет для сетки
            pane.XAxis.MajorGrid.Color = Color.White;
            pane.YAxis.MajorGrid.Color = Color.White;

            // Добавим кривую пока еще без каких-либо точек
            LineItem curve1 = pane.AddCurve("", _data, Color.Red, SymbolType.None);
            LineItem curve2 = pane.AddCurve("", _data2, Color.Yellow, SymbolType.None);
            LineItem curve3 = pane.AddCurve("", _data3, Color.Purple, SymbolType.None);

            // толщина линии графика
            curve1.Line.Width = 2.0F;
            curve2.Line.Width = 2.0F;
            curve3.Line.Width = 2.0F;

            // название заголовков
            pane.Title.Text = "Графики";
            pane.XAxis.Title.Text = "время [сек]";
            pane.YAxis.Title.Text = "Среднее значение";

            // Включаем отображение сетки напротив мелких рисок по оси X
            pane.XAxis.MinorGrid.IsVisible = true;

            // Устанавливает цвет для сетки напротив мелких рисок по оси X
            pane.XAxis.MinorGrid.Color = Color.White;

            // Меняем количество мелких рисок  между крупными рисками
            pane.XAxis.Scale.MinorStep = 10.0 * _step;

            // Меняем длину пропусков/пробелов в сетке (пикселя - пропуск) 
            pane.XAxis.MinorGrid.DashOff = 35;

            // Вызываем метод AxisChange (), чтобы обновить данные об осях. 
            zedGraph2.AxisChange();

            // Обновляем график
            zedGraph2.Invalidate();
        }
        #endregion

        #region Основные Параметры
        int _capacity = 500; // Длина массива( _data , _data2 , _data3 )

        // Текущее значение на графике
        double _currentx = 0; // Первая точка масива/графика по оси х
        //double _currentx1 = 0;
        //double _currentx2 = 0;
        double _step = 0.1; //шаг с которым будет менятся точки масива/графика
        /// <summary>
        /// Здесь храним данные
        /// </summary>
        RollingPointPairList _data;
        RollingPointPairList _data2;
        RollingPointPairList _data3;
        #endregion

        #region Timer+GraphRefresh functions
        private void timer1_Tick(object sender, EventArgs e)
        {
            _currentx += _step; // Значени по оси х (время)
            // Рассчитаем интервал по оси X, который нужно отобразить на графике
            double xmin = _currentx - _capacity * _step;
            double xmax = _currentx;

            if (checkBox1.Checked)
            {
                numericUpDown1.Value = (int)_in1;
                _data.Add(_currentx, _in1);
            }

            if (checkBox2.Checked)
            {
                numericUpDown2.Value = (int)_in2;
                _data2.Add(_currentx, _in2);
            }

            if (checkBox3.Checked)
            {
                numericUpDown3.Value = (int)_in3;
                _data3.Add(_currentx, _in3);
            }

            GraphPane pane = zedGraph2.GraphPane;
            pane.XAxis.Scale.Min = xmin;
            pane.XAxis.Scale.Max = xmax;


            if (!_stpStr)   // Если не нажата кнопка паузы
            {
                // Обновим оси
                zedGraph2.AxisChange();

                // Обновим сам график
                zedGraph2.Invalidate();
            }
        }
        #endregion

        #region Обработчик превышения zoom'а по осям Х и Y
        void zedGraph2_ZoomEvent(ZedGraphControl sender, ZoomState oldState, ZoomState newState)
        {
            // Максимльное и минимальное значени шкалы Y

            GraphPane pane = sender.GraphPane;

            if (pane.YAxis.Scale.Min <= 0)
            {
                pane.YAxis.Scale.Min = 0;
            }

            if (pane.YAxis.Scale.Max >= 1200)
            {
                pane.YAxis.Scale.Max = 1200;
            }
        }

        #endregion

        #region Кнопка фиксации
        private void button1_Click(object sender, EventArgs e)
        {
            // Останавливает текущее изображение графика / Останавливает обновление изображения графика  ( но сам график продолжает записоватся в массив при нажатие продолжить он его тутже выведет на экран )
            if (!_stpStr)
            { _stpStr = true; button1.BackColor = Color.LightGreen; button1.Text = "Продолжить"; }
            else
            { _stpStr = false; button1.BackColor = Color.Khaki; button1.Text = "Зафиксировать"; }
        }
        #endregion 

        #region closingoptions
        private void Form3_FormClosing(object sender, FormClosingEventArgs e)
        {
            this.Hide();
            Frm3Form1.inv1.U = 10; // dbg
            Frm3Form1.FlagGrphOpen = false;
            e.Cancel = true; // Cancel closing
        }
        #endregion



        private void numericUpDown4_ValueChanged(object sender, EventArgs e)
        {
            // Скорость обновления графика в миллисекундах. 15.6 мс - самое быстрое, что может таймер (ограничение из-за винды)
            timer1.Interval = (int)numericUpDown4.Value;
        }

        public void numericUpDown5_ValueChanged(object sender, EventArgs e)
        {
            //Шаг, с которым будет обновляться график
            _step = Convert.ToDouble(numericUpDown5.Value);
            zedGraph2.GraphPane.XAxis.Scale.MinorStep = 10.0 * _step;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            // Очищает график и запускает с нулувой точки
            _currentx = 0;
            _data.Clear();
            _data2.Clear();
            _data3.Clear();
        }

    }
}
