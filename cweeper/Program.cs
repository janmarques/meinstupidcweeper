using System;
using System.Collections.Generic;
using System.Linq;

namespace cweeper
{
    class Program
    {
        static void Main(string[] args)
        {

            var game = new Game();
            game.Setup(10, 10, 5);

            while (true)
            {
               

                var analysis = new GameAnalysis(game);
                var certainActions = analysis.GetCertainActions().ToList();
                if (!certainActions.Any())
                {
                    var randomCell = game.GetRandomMove();
                    game.ClickCell(randomCell);
                }
                else
                {
                    foreach (var action in certainActions)
                    {
                        var cell = game.Board.Single(x => x.X == action.X && x.Y == action.Y);
                        if (action.ShouldClick)
                        {
                            game.ClickCell(cell);
                        }
                        else
                        {
                            game.FlagCell(cell);
                        }
                    }
                }

                PrintBoard(game);
                if(game.GameState != GameState.Running)
                {
                    Console.ReadLine();
                    game = new Game();
                    game.Setup(10, 10, 5);
                }
            }

        }

        private static void PrintBoard(Game game)
        {
            var dashLine = new String('-', game.Width * 2 + 1);
            Console.WriteLine(dashLine);
            for (int x = 0; x < game.Height; x++)
            {
                Console.Write("|");

                for (int y = 0; y < game.Width; y++)
                {
                    var cell = game.Board.Single(z => z.X == x && z.Y == y);
                    Console.Write(cell);
                    Console.Write("|");
                }
                Console.WriteLine();
                Console.WriteLine(dashLine);
            }

            Console.WriteLine(game.GameState.ToString());
        }
    }

    enum GameState { Running, Won, Lost }
    class Game
    {
        public List<Cell> Board;
        public int Width;
        public int Height;
        public GameState GameState;

        public void Setup(int width, int height, int bombs)
        {
            GameState = GameState.Running;
            Width = width;
            Height = height;
            Board = new List<Cell>();
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    Board.Add(new Cell(x, y));
                }
            }
            foreach (var cell in Board)
            {
                cell.NeighbouringCells = GetNeighbouringCells(cell);
            }
            foreach (var bombCell in Board.OrderBy(x => Guid.NewGuid()).Take(bombs))
            {
                bombCell.SetBomb();
            }
        }

        public void ClickCell(Cell c)
        {
            if (c.IsClicked) { return; }
            c.Click();
            if (c.IsBomb)
            {
                GameState = GameState.Lost;
                return;
            }
            if (c.IsEmpty)
            {
                foreach (var neighbour in c.NeighbouringCells)
                {
                    ClickCell(neighbour);
                }
            }
            if (Board.Where(x => !x.IsClicked).All(x => x.IsBomb))
            {
                GameState = GameState.Won;
            }

        }

        public void FlagCell(Cell c)
        {
            c.Flag();

        }


        public Cell GetRandomMove() => 
            
            Board.Where(x => !x.IsClicked).OrderBy(x => Guid.NewGuid()).First();

        private List<Cell> GetNeighbouringCells(Cell referenceCell)
        {
            var cells = new List<Cell>();
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


    class Cell
    {
        public Cell(int x, int y)
        {
            X = x;
            Y = y;
        }


        public void SetBomb() => IsBomb = true;
        public void Click()
        {
            IsFlagged = false;
            IsClicked = true;
        }
        public void Flag() => IsFlagged = true;

        public bool IsFlagged { get; set; }
        public int X { get; }
        public int Y { get; }
        public bool IsClicked { get; private set; }
        public bool IsBomb { get; private set; }
        public List<Cell> NeighbouringCells { get; set; }
        public int NeighbouringBombCount => NeighbouringCells.Count(x => x.IsBomb);
        public bool IsEmpty => NeighbouringBombCount == 0;

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

        public GameAnalysis(Game game)
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
                        yield return new CellAction { ShouldClick = true, X = safeCell.X, Y = safeCell.Y };
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
