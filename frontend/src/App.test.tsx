import { createElement } from 'react'
import { renderToStaticMarkup } from 'react-dom/server'
import { describe, expect, it } from 'vitest'
import App from './App'

describe('App', () => {
  it('renders the initial welcome page', () => {
    const markup = renderToStaticMarkup(createElement(App))

    expect(markup).toContain('Plataforma Educacional')
    expect(markup).toContain('Fundação do repositório')
  })
})
