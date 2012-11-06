using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Timers;

namespace LifeGameWindow
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        #region 型定義
        /// <summary>
        /// セル位置を保持する構造体
        /// </summary>
        class CellPoint
        {
            public int column;
            public int row;
        }

        /// <summary>
        /// 別スレッドからセルの生死を設定する用のデリゲータ
        /// </summary>
        /// <param name="rect"></param>
        /// <param name="isLife"></param>
        delegate void SetCellToLifeDeadDeligate(Rectangle rect, bool isLife);
        #endregion

        #region 定数
        /// <summary>
        /// Life領域の1セルのサイズ [pixcel] (-)
        /// </summary>
        private const int LifeCellSize = 15;

        /// <summary>
        /// 死んでいるセルの色 (defaultのセル色) [-] (-)
        /// </summary>
        private SolidColorBrush DeadCellColor = Brushes.White;

        /// <summary>
        /// 生きているセルの色 [-] (-)
        /// </summary>
        private SolidColorBrush LifeCellColor = Brushes.GreenYellow;

        /// <summary>
        /// 再生時のインターバル [msec] (-)
        /// </summary>
        private const int PlayInterval = 500;
        #endregion

        #region メンバ
        /// <summary>
        /// 再生中フラグ [-] (true:再生, false:停止)
        /// </summary>
        private bool _isPlay = false;

        /// <summary>
        /// ユーザによる任意配置フラグ [-] (true:任意配置中, false:任意配置中ではない)
        /// </summary>
        private bool _isSetUserPos = false;

        /// <summary>
        /// "生"セルのリスト
        /// </summary>
        private List<CellPoint> _lifeCellList;

        /// <summary>
        /// セルのGUIコントロールリスト
        /// </summary>
        private List<List<Rectangle>> _cellControlList;

        private Random _rand;
        private Timer _playTimer;
        #endregion

        #region コンストラクション
        /// <summary>
        /// コンストラクタ
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();

            _lifeCellList = new List<CellPoint>();
            _rand = new Random();
            _playTimer = new Timer();
            _playTimer.Interval = PlayInterval;
            _playTimer.Elapsed += new ElapsedEventHandler(PlayTimer_Elapsed);
        }
        #endregion

        #region メソッド
        /// <summary>
        /// 指定したセルの生死を反転する。
        /// </summary>
        /// <param name="cell"></param>
        private void InverseCellLifeVsDead(Rectangle cell)
        {
            SetCellToLifeDead(cell, (cell.Fill == DeadCellColor) ? true : false);
        }

        /// <summary>
        /// 指定したセルの生死を設定する
        /// </summary>
        /// <param name="cell">対象セル [-] (-)</param>
        /// <param name="isLife">設定する生死 [-] (true:生, false:死)</param>
        private void SetCellToLifeDead(Rectangle cell, bool isLife)
        {
            int cellRow = Grid.GetRow(cell);
            int cellCol = Grid.GetColumn(cell);

            CellPoint cellPos = _lifeCellList.Find(node => ((node.column == cellCol) && (node.row == cellRow)));

            if (isLife)
            {
                cell.Fill = LifeCellColor;

                // 生セルのリストに座標を登録
                if (null == cellPos)
                {
                    _lifeCellList.Add(new CellPoint() { row = cellRow, column = cellCol });
                }
            }
            else
            {
                cell.Fill = DeadCellColor;

                // 生セルのリストから座標を削除
                if (null != cellPos)
                {
                    _lifeCellList.RemoveAll(node => ((node.column == cellCol) && (node.row == cellRow)));
                }
            }
        }

        /// <summary>
        /// 任意配置の開始／終了状態を指定した状態に変更する。
        /// </summary>
        /// <param name="state">設定する状態 [-] (true:任意配置中, false:任意配置終了)</param>
        private void ChangeUserSetPosState(bool state)
        {
            _isSetUserPos = state;

            // メニュー, ツールバーの任意は位置状態をstateに合わせる
            myMenuUserSet.IsChecked = state;
            myToolUserSet.IsChecked = state;
        }

        /// <summary>
        /// "生"のセルリストをベースに次周期のセルの生死状態を計算
        /// </summary>
        /// <param name="lifeArea">次周期のセルの状態の格納先 [-] (-)</param>
        private void calcNextState(ref int[,] lifeArea)
        {
            foreach (var cellPos in _lifeCellList)
            {
                int[] r = new int[] {
                    cellPos.row,
                    cellPos.row + 1,
                    cellPos.row + 2
                };
                int[] c = new int[] {
                    cellPos.column,
                    cellPos.column + 1,
                    cellPos.column + 2
                };

                // cellPos自身のセルには +1, 周囲のセルには +10 する
                lifeArea[r[0], c[0]] += 10;
                lifeArea[r[0], c[1]] += 10;
                lifeArea[r[0], c[2]] += 10;

                lifeArea[r[1], c[0]] += 10;
                lifeArea[r[1], c[1]] += 1;
                lifeArea[r[1], c[2]] += 10;

                lifeArea[r[2], c[0]] += 10;
                lifeArea[r[2], c[1]] += 10;
                lifeArea[r[2], c[2]] += 10;
            }
        }

        /// <summary>
        /// 2次元の生死状態配列(index=-1, max+1分拡張したもの)に合わせて画面のライフ領域を更新
        /// </summary>
        /// <param name="lifeArea">生死状態の2次元配列 [-] (-)</param>
        private void updateLifeAreaByExtendedAreaArray(int[,] lifeArea)
        {
            SetCellToLifeDeadDeligate setCellState = SetCellToLifeDead;

            _lifeCellList.Clear();

            for (int r = 1; r < lifeArea.GetLength(0) - 1; ++r)
            {
                for (int c = 1; c < lifeArea.GetLength(1) - 1; ++c)
                {
                    bool isLife = false;

                    // 前周期に生きているセルに対して、そのセル自身: +1、周囲のセル: +10 としたため
                    //  num = 21, 31 (周囲に2 or 3つ生セル + 自身が生)
                    //        30 (周囲に3つの生セル + 自身が死)
                    // のとき、生のセルとなる
                    switch (lifeArea[r, c])
                    {
                        case 21:
                        case 31:
                        case 30:
                            isLife = true;
                            break;
                        default:
                            isLife = false;
                            break;
                    }

                    Dispatcher.Invoke(setCellState, new object[] { _cellControlList[r - 1][c - 1], isLife });
                }
            }
        }
        #endregion

        #region イベントハンドラ
        /// <summary>
        /// 再生用タイマーの時刻経過時イベントハンドラ
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void PlayTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            // 表示領域のセル数に合わせたキャンバスを作成
            int rowCnt = myGridLifeArea.RowDefinitions.Count + 2;    // +2 は外側(index=-1, index=max+1)用
            int colCnt = myGridLifeArea.ColumnDefinitions.Count + 2; // +2 は外側(index=-1, index=max+1)用
            int[,] lifeArea = new int[rowCnt, colCnt];

            // 前周期の生セルから次周期の生死状態を計算
            calcNextState(ref lifeArea);

            // 生死状態を画面に反映
            updateLifeAreaByExtendedAreaArray(lifeArea);
        }

        /// <summary>
        /// Playコマンド実行時処理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PlayCmd_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (_isSetUserPos)
            {
                // 任意配置を終了
                ChangeUserSetPosState(false);
            }

            // 配置メニューを非活性化
            myToolBarSetPos.IsEnabled = false;
            myMenuRandom.IsEnabled = false;
            myMenuUserSet.IsEnabled = false;
            myMenuClear.IsEnabled = false;

            _isPlay = true;
            _playTimer.Start();
        }

        /// <summary>
        /// Playコマンドの実行可否判定
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PlayCmd_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = (!_isPlay);
        }

        /// <summary>
        /// Stopコマンド実行時処理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void StopCmd_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            _playTimer.Stop();
            _isPlay = false;

            // 配置メニューを活性化
            myToolBarSetPos.IsEnabled = true;
            myMenuRandom.IsEnabled = true;
            myMenuUserSet.IsEnabled = true;
            myMenuClear.IsEnabled = true;
        }

        /// <summary>
        /// Stopコマンドの実行可否判定
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void StopCmd_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = _isPlay;
        }

        /// <summary>
        /// 画面ロード時のイベントハンドラ
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            double height = myGridLifeArea.ActualHeight;
            double width = myGridLifeArea.ActualWidth;
            double calcSize = System.Math.Min(height, width);
            int cellCnt = (int)( calcSize / LifeCellSize );

            // Life領域のパネル作成: Grid作成
            for (int i = 0; i < cellCnt; ++i)
            {
                RowDefinition row = new RowDefinition();
                row.Height = new GridLength(1, GridUnitType.Star);

                ColumnDefinition col = new ColumnDefinition();
                col.Width = new GridLength(1, GridUnitType.Star);

                myGridLifeArea.RowDefinitions.Add(row);
                myGridLifeArea.ColumnDefinitions.Add(col);
            }

            _cellControlList = new List<List<Rectangle>>();
            for (int r = 0; r < myGridLifeArea.RowDefinitions.Count; ++r)
            {
                List<Rectangle> cellList = new List<Rectangle>();

                for (int c = 0; c < myGridLifeArea.ColumnDefinitions.Count; ++c)
                {
                    Rectangle rect = new Rectangle();
                    rect.Fill = DeadCellColor;
                    rect.Margin = new Thickness(1, 1, 1, 1);
                    rect.MouseEnter += new MouseEventHandler(LifeCell_MouseEnter);
                    rect.MouseLeftButtonDown += new MouseButtonEventHandler(LifeCell_MouseLeftButtonDown);

                    myGridLifeArea.Children.Add(rect);
                    Grid.SetRow(rect, r);
                    Grid.SetColumn(rect, c);

                    cellList.Add(rect);
                }

                _cellControlList.Add(cellList);
            }
        }

        /// <summary>
        /// Lifeセルでマウス移動時のイベントハンドラ: (左ボタンPress時)マウス移動による任意配置
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void LifeCell_MouseEnter(object sender, MouseEventArgs e)
        {
            if (_isSetUserPos)
            {
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    InverseCellLifeVsDead(sender as Rectangle);
                }
            }
        }

        /// <summary>
        /// Lifeセルでマウス左ボタンが押されたときのイベントハンドラ: 個々のセルに対する任意配置
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void LifeCell_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_isSetUserPos)
            {
                InverseCellLifeVsDead(sender as Rectangle);
            }
        }

        /// <summary>
        /// ランダム配置(メニュー、ツールバー)クリック時イベントハンドラ：ランダムで生死セルの配置
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RandomSet_Click(object sender, RoutedEventArgs e)
        {
            int randMax = 10000;
            double lifeCellRate = 0.2;
            int lifeCellNum = (int)(randMax * lifeCellRate);

            // 生セルリストをクリア
            _lifeCellList.Clear();

            // ランダムで生セルを作成
            foreach (var rect in myGridLifeArea.Children)
            {
                bool isLife = false;

                if (_rand.Next(randMax) <= lifeCellNum)
                {
                    isLife = true;
                }

                SetCellToLifeDead(rect as Rectangle, isLife);
            }
        }

        /// <summary>
        /// 任意配置(メニュー, ツールバー)クリック時イベントハンドラ: 任意配置の開始／終了
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UserSet_Click(object sender, RoutedEventArgs e)
        {
            // 任意配置の開始／終了を反転
            ChangeUserSetPosState(!_isSetUserPos);
        }

        /// <summary>
        /// クリア(メニュー、ツール）クリック時イベントハンドラ：ライフ領域のクリア
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ClearSet_Click(object sender, RoutedEventArgs e)
        {
            // 生セルリストをクリア
            _lifeCellList.Clear();

            // 表示をクリア
            foreach (var rect in myGridLifeArea.Children)
            {
                SetCellToLifeDead(rect as Rectangle, false);
            }
        }

        /// <summary>
        /// メニュー:終了選択時イベントハンドラ
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void myMenuQuit_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
        #endregion
    }
}
