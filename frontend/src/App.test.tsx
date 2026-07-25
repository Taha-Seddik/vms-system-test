import { render, screen } from '@testing-library/react'
import { ThemeProvider } from '@mui/material'
import App from './App'
import { theme } from './theme'

describe('App', () => {
  it('shows the Step 1 foundation and four generated cameras', () => {
    render(
      <ThemeProvider theme={theme}>
        <App />
      </ThemeProvider>,
    )

    expect(
      screen.getByRole('heading', { name: /VMS Command Center/i }),
    ).toBeInTheDocument()
    expect(screen.getAllByText('HLS configured')).toHaveLength(4)
    expect(screen.getByText('Entrance')).toBeInTheDocument()
    expect(screen.getByText('Warehouse')).toBeInTheDocument()
  })
})

