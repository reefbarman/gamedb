namespace GameDBEditorLibrary
{
    internal class GameDBDataSource
    {
        private GameDB m_gameDB = null;

        public GameDB GameDB => m_gameDB;

        public void UpdateSource(GameDB gameDb)
        {
            m_gameDB = gameDb;
        }
    }
}
