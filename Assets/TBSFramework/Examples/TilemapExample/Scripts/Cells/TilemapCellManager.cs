using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TurnBasedStrategyFramework.Common.Cells;
using TurnBasedStrategyFramework.Common.Controllers;
using TurnBasedStrategyFramework.Common.Utilities;
using TurnBasedStrategyFramework.Unity.Cells;
using TurnBasedStrategyFramework.Unity.Utilities;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;

namespace TurnBasedStrategyFramework.Unity.Examples.TilemapExample.Cells
{
    public class TilemapCellManager : UnityCellManager
    {
        [SerializeField] private Camera _mainCamera;

        public override event Action<ICell> CellAdded;
        public override event Action<ICell> CellRemoved;

        [SerializeField] Tilemap _dataLayer;
        [SerializeField] Tilemap _highlightLayer;
        [SerializeField] Tile _highlightTile;
        [SerializeField] Tile _reachableTile;
        [SerializeField] Tile _pathTile;

        Dictionary<IVector2Int, VirtualSquareCell> _cells;

        private VirtualSquareCell _selectedCell;
        private float _lastRaycast = 0;
        [SerializeField] private float _raycastDelay = 0.1f;
        [SerializeField] private Tile _lineHorizontal;
        [SerializeField] private Tile _lineVertical;
        [SerializeField] private Tile _curlLowerRight;
        [SerializeField] private Tile _curlUpperLeft;
        [SerializeField] private Tile _curlLowerLeft;
        [SerializeField] private Tile _curlUpperRight;
        [SerializeField] private Tile _arrowLeft;
        [SerializeField] private Tile _arrowRight;
        [SerializeField] private Tile _arrowUp;
        [SerializeField] private Tile _arrowDown;

        public override void Initialize(IGridController gridController)
        {
            BoundsInt bounds = _dataLayer.cellBounds;
            _cells = new Dictionary<IVector2Int, VirtualSquareCell>();

            foreach (Vector3Int pos in bounds.allPositionsWithin)
            {
                DataTile tile = _dataLayer.GetTile<DataTile>(pos);
                if (tile == null)
                {
                    continue;
                }
                var worldPosition = _dataLayer.GetCellCenterWorld(pos).ToIVector3();
                var gridPosition = new Vector2IntImpl(pos.x, pos.y);
                var cell = new VirtualSquareCell(gridPosition, worldPosition, tile.movementCost, false, tile.cellType);
                _cells.Add(gridPosition, cell);
                CellAdded?.Invoke(cell);
            }

            _selectedCell = _cells.Values.First();
        }

        public override ICell GetCellAt(IVector2Int coords)
        {
            if (_cells.TryGetValue(coords, out var cell))
            {
                return cell;
            }
            return null;
        }

        void Update()
        {
            if (Input.GetMouseButtonDown(0))
            {
                var cell = TryGetCellUnderCursor();
                if (cell != null)
                {
                    cell.OnMouseDown();
                    return;
                }
            }

            var currentTime = Time.time;
            if (currentTime - _lastRaycast < _raycastDelay)
            {
                return;
            }

            _lastRaycast = currentTime;

            var highlightedCell = TryGetCellUnderCursor();
            if (_selectedCell != highlightedCell)
            {
                _selectedCell?.OnMouseExit();
                highlightedCell?.OnMouseEnter();
                _selectedCell = highlightedCell;
            }
        }

        private VirtualSquareCell TryGetCellUnderCursor()
        {
            Vector3 mouseScreenPos = Input.mousePosition;
            Vector3 screenPoint = new Vector3(mouseScreenPos.x, mouseScreenPos.y, 0);
            Vector3 mouseWorldPos = _mainCamera.ScreenToWorldPoint(screenPoint);
            Vector3Int cellPos = _dataLayer.WorldToCell(mouseWorldPos);

            var gridPosition = new Vector2IntImpl(cellPos.x, cellPos.y);

            if (!_cells.TryGetValue(gridPosition, out var cell))
            {
                return null;
            }

            Vector3 cellWorldCenter = _dataLayer.GetCellCenterWorld(cellPos);

            Collider2D[] colliders2D = Physics2D.OverlapPointAll(cellWorldCenter);
            if (colliders2D.Any(c => !c.isTrigger))
            {
                return null;
            }

            float checkRadius = 0.1f;
            Collider[] colliders3D = Physics.OverlapSphere(cellWorldCenter, checkRadius);
            if (colliders3D.Any(c => !c.isTrigger))
            {
                return null;
            }

            return cell;
        }

        public override IEnumerable<ICell> GetCells()
        {
            return _cells.Values;
        }

        public override async Task MarkAsReachable(IEnumerable<ICell> cells)
        {
            foreach (var cell in cells)
            {
                await MarkAsReachable(cell);
            }
        }

        public override Task MarkAsReachable(ICell cell)
        {
            _highlightLayer.SetTile(new Vector3Int(cell.GridCoordinates.x, cell.GridCoordinates.y, 0), _reachableTile);
            return Task.CompletedTask;
        }

        public override Task MarkAsHighlighted(ICell cell)
        {
            _highlightLayer.SetTile(new Vector3Int(cell.GridCoordinates.x, cell.GridCoordinates.y, 0), _highlightTile);
            return Task.CompletedTask;
        }

        public override Task UnMarkAsHighlighted(ICell cell)
        {
            return UnMark(cell);
        }

        public override async Task UnMark(IEnumerable<ICell> cells)
        {
            foreach (var cell in cells)
            {
                await UnMark(cell);
            }
        }

        public override Task UnMark(ICell cell)
        {
            _highlightLayer.SetTile(new Vector3Int(cell.GridCoordinates.x, cell.GridCoordinates.y, 0), null);
            return Task.CompletedTask;
        }

        public override Task MarkAsPath(IEnumerable<ICell> cells, ICell originCell)
        {
            int i = 0;
            var path = cells.ToList();
            foreach (var cell in cells)
            {
                var index = i;
                var currentPosition = path[index].GridCoordinates;

                IVector2Int prevPosition = index > 0 ? path[index - 1].GridCoordinates : originCell.GridCoordinates;
                IVector2Int nextPosition = index < path.Count - 1 ? path[index + 1].GridCoordinates : currentPosition;

                var selectedTile = index == path.Count - 1
                    ? GetArrowHeadSprite(prevPosition, currentPosition)
                    : GetArrowSegmentSprite(prevPosition, currentPosition, nextPosition);

                _highlightLayer.SetTile(new Vector3Int(cell.GridCoordinates.x, cell.GridCoordinates.y, 0), selectedTile);
                i++;
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// Determines the appropriate arrow segment sprite to use for a given segment of the path based on the relative positions of the previous, current, and next cells.
        /// </summary>
        /// <param name="origin">The origin point of the previous cell.</param>
        /// <param name="first">The current cell position.</param>
        /// <param name="second">The next cell position.</param>
        /// <returns>The sprite corresponding to the correct directional segment of the path.</returns>
        private Tile GetArrowSegmentSprite(IVector2Int origin, IVector2Int first, IVector2Int second)
        {
            if (second.y == origin.y)
                return _lineHorizontal;
            if (second.x == origin.x)
                return _lineVertical;

            if (origin.y > second.y)
            {
                return origin.x > second.x
                    ? (origin.y != first.y ? _curlLowerRight : _curlUpperLeft)
                    : (origin.y != first.y ? _curlLowerLeft : _curlUpperRight);
            }
            else
            {
                return origin.x > second.x
                    ? (origin.y != first.y ? _curlUpperRight : _curlLowerLeft)
                    : (origin.y != first.y ? _curlUpperLeft : _curlLowerRight);
            }
        }
        /// <summary>
        /// Determines the appropriate arrowhead sprite to use at the end of the path based on the direction between the last two cells.
        /// </summary>
        /// <param name="from">The position of the second-to-last cell in the path.</param>
        /// <param name="to">The position of the last cell in the path.</param>
        /// <returns>The sprite corresponding to the correct arrowhead direction.</returns>
        private Tile GetArrowHeadSprite(IVector2Int from, IVector2Int to)
        {
            if (to.x != from.x)
                return to.x < from.x ? _arrowLeft : _arrowRight;
            return to.y <= from.y ? _arrowDown : _arrowUp;
        }

        public override void SetColor(ICell cell, float r, float g, float b, float a)
        {
        }
    }
}