using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Interactions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace cweeper
{
    class Program
    {

        static async Task Main(string[] args)
        {
            var browser = new ChromeDriver();
            browser.Navigate().GoToUrl("https://minesweeper.online/game/263993505");

            Console.ReadLine();
            browser.FindElementById("top_area_face").Click();

            Console.ReadLine();

            while (true)
            {
                if (browser.FindElementsByClassName("hd_top-area-face-win").Any())
                {
                    Console.ReadLine();
                    browser.FindElementById("top_area_face").Click();

                    Console.ReadLine();
                }
                var htmlCells = browser.FindElementsByClassName("cell").ToList();

                var onlineGame = new OnlineGame(htmlCells);

                var analysis = new GameAnalysis(onlineGame);
                var certainActions = analysis.GetCertainActions().ToList();
                if (!certainActions.Any())
                {
                    var ngStart = browser.FindElementsByClassName("start").SingleOrDefault();
                    if (ngStart != null)
                    {
                        ngStart.Click();
                    }
                    else
                    {
                        var randomCell = analysis.Board.Where(x => !x.Discovered && !x.Flag).OrderBy(x => Guid.NewGuid()).First();
                        var cell = browser.FindElementById($"cell_{randomCell.X}_{randomCell.Y}");
                        cell.Click();
                    }
                }
                else
                {
                    foreach (var action in certainActions)
                    {
                        var cell = browser.FindElementById($"cell_{action.X}_{action.Y}");
                        if (action.ShouldClick)
                        {
                            cell.Click();
                            await Task.Delay(50);
                        }
                        else
                        {
                            var rightClickAction = new Actions(browser);
                            rightClickAction.ContextClick(cell);
                            rightClickAction.Perform();
                        }
                    }
                }
            }

        }
    }

    class OnlineGame : IGame
    {
        public OnlineGame(List<IWebElement> htmlCells)
        {
            Board = new List<ICell>();
            foreach (var cell in htmlCells)
            {
                var id = cell.GetAttribute("id");
                var x = int.Parse(cell.GetAttribute("data-x"));
                var y = int.Parse(cell.GetAttribute("data-y"));
                var classes = cell.GetAttribute("class");
                var isDiscovered = classes.Contains("hd_opened");
                var isFlag = classes.Contains("hd_flag");
                var bombCount = -1;
                if (classes.Contains("hd_type"))
                {
                    for (int i = 0; i < 9; i++)
                    {
                        if (classes.Contains("hd_type" + i))
                        {
                            bombCount = i;
                            break;
                        }
                    }
                }

                Board.Add(new OnlineCell
                {
                    X = x,
                    Y = y,
                    IsBomb = false,
                    IsClicked = isDiscovered,
                    IsFlagged = isFlag,
                    IsEmpty = bombCount == 0,
                    NeighbouringBombCount = bombCount
                });

            }
        }

        public List<ICell> Board { get; set; }
    }

    enum GameState { Running, Won, Lost }

    internal interface IGame
    {
        List<ICell> Board { get; set; }
    }

    interface ICell
    {
        bool IsBomb { get; }
        bool IsClicked { get; }
        bool IsEmpty { get; }
        bool IsFlagged { get; set; }
        int NeighbouringBombCount { get; }
        int X { get; }
        int Y { get; }
    }

    public class OnlineCell : ICell
    {
        public bool IsBomb { get; set; }
        public bool IsClicked { get; set; }
        public bool IsEmpty { get; set; }
        public bool IsFlagged { get; set; }
        public int NeighbouringBombCount { get; set; }
        public int X { get; set; }
        public int Y { get; set; }

        public override string ToString()
        {
            if (IsFlagged) { return "F"; }
            if (!IsClicked) { return " "; }
            if (IsBomb) { return "B"; }
            return NeighbouringBombCount.ToString();
        }
    }

    class GameAnalysis
    {
        public List<AnalysisCell> Board;

        public GameAnalysis(IGame game)
        {
            Board = new List<AnalysisCell>();
            foreach (var cell in game.Board)
            {
                var analysisCell = new AnalysisCell { X = cell.X, Y = cell.Y };
                if (cell.IsClicked)
                {
                    analysisCell.Count = cell.NeighbouringBombCount;
                    analysisCell.Discovered = true;
                }
                else if (cell.IsFlagged)
                {
                    analysisCell.Flag = true;
                }
                Board.Add(analysisCell);
            }
            foreach (var cell in Board)
            {
                cell.NeighbouringCells = GetNeighbouringCells(cell);
            }
        }

        public IEnumerable<CellAction> GetCertainActions()
        {
            for (int i = 0; i < 1; i++)
            {
                foreach (var discoveryEdge in Board.Where(x => x.Discovered)) // improve where clause?
                {
                    // flag
                    var neighbouringPotentialBombs = discoveryEdge.NeighbouringCells.Where(x => !x.Discovered);
                    if (neighbouringPotentialBombs.Count() == discoveryEdge.Count)
                    {
                        foreach (var bomb in neighbouringPotentialBombs.Where(x => !x.Flag))
                        {
                            bomb.Flag = true;
                            yield return new CellAction { ShouldClick = false, X = bomb.X, Y = bomb.Y };
                        }
                    }

                    // click
                    var neighbouringFlags = discoveryEdge.NeighbouringCells.Where(x => x.Flag);
                    if (neighbouringFlags.Count() == discoveryEdge.Count)
                    {
                        var neighbouringUndiscoveredNonFlags = discoveryEdge.NeighbouringCells.Where(x => !x.Discovered && !x.Flag);
                        foreach (var safeCell in neighbouringUndiscoveredNonFlags)
                        {
                            safeCell.Discovered = true;
                            yield return new CellAction { ShouldClick = true, X = safeCell.X, Y = safeCell.Y };
                        }
                    }
                }
            }
        }

        private List<AnalysisCell> GetNeighbouringCells(AnalysisCell referenceCell)
        {
            var cells = new List<AnalysisCell>();
            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    var neighbouringCell = Board.SingleOrDefault(z => z.X == (referenceCell.X + x) && z.Y == (referenceCell.Y + y));
                    if (neighbouringCell != null && neighbouringCell != referenceCell)
                    {
                        cells.Add(neighbouringCell);
                    }
                }
            }
            return cells;
        }
    }

    public class CellAction
    {
        public int X;
        public int Y;
        public bool ShouldClick;
    }

    class AnalysisCell
    {
        public int X;
        public int Y;
        public int Count;
        public bool Discovered;
        public bool Flag;

        public List<AnalysisCell> NeighbouringCells { get; set; }
    }
}
