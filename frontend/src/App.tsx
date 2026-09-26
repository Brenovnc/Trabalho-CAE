import styles from './App.module.css'

function App() {
  return (
    <main className={styles.page}>
      <section className={styles.card} aria-labelledby="welcome-title">
        <p className={styles.eyebrow}>Fundação do repositório</p>
        <h1 id="welcome-title">Plataforma Educacional</h1>
        <p className={styles.description}>
          A base da aplicação está pronta para as próximas etapas de desenvolvimento.
        </p>
      </section>
    </main>
  )
}

export default App
