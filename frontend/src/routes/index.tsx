import { createFileRoute } from '@tanstack/react-router'
import { useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import aspireLogo from '/Aspire.png'
import { useWeatherForecast, useSecureWeatherForecast, useAuth, useAuthActions } from '../api/hooks'

export const Route = createFileRoute('/')({
  component: Index,
})

function Index() {
  const [useCelsius, setUseCelsius] = useState(false)
  const [showSecure, setShowSecure] = useState(false)
  const queryClient = useQueryClient()
  const auth = useAuth()
  const { login, logout } = useAuthActions()
  const { data: weatherData = [], isLoading: loading, error, isFetching } = useWeatherForecast()
  const { data: secureWeatherData = [], isLoading: secureLoading, error: secureError, isFetching: secureFetching } = useSecureWeatherForecast(showSecure && auth.isLoggedIn)

  const handleRefresh = () => {
    queryClient.invalidateQueries({ queryKey: ['weather'] })
  }

  const isCurrentlyFetching = showSecure && auth.isLoggedIn ? secureFetching : isFetching

  const formatDate = (dateString: string) => {
    return new Date(dateString).toLocaleDateString(undefined, {
      weekday: 'short',
      month: 'short',
      day: 'numeric',
    })
  }

  const handleLogin = () => {
    login('/')
  }

  const handleLogout = () => {
    if (auth.logoutUrl) {
      logout(auth.logoutUrl)
    }
  }

  return (
    <div className="w-full min-h-screen flex flex-col bg-linear-to-br from-bg-gradient-start to-bg-gradient-end text-text-primary">
      {/* Header */}
      <header className="px-8 pt-10 pb-6 text-center animate-fade-in-down max-md:px-4 max-md:pt-6 max-md:pb-4">
        <div className="flex justify-between items-center max-w-[1400px] mx-auto px-4">
          <a
            href="https://aspire.dev"
            target="_blank"
            rel="noopener noreferrer"
            aria-label="Visit Aspire website (opens in new tab)"
            className="inline-block rounded-lg focus-visible:outline-3 focus-visible:outline-accent-end focus-visible:outline-offset-8"
          >
            <img
              src={aspireLogo}
              className="h-20 w-auto transition-all duration-300 drop-shadow-lg hover:scale-110 hover:rotate-5 hover:drop-shadow-xl max-md:h-12"
              alt="Aspire logo"
            />
          </a>
          <div className="flex items-center gap-4">
            {auth.isLoading ? (
              <span className="text-text-tertiary text-sm">Loading...</span>
            ) : auth.isLoggedIn ? (
              <>
                <span className="text-text-secondary text-sm font-medium">Welcome, {auth.username}</span>
                <button
                  className="px-5 py-2 rounded-lg text-sm font-semibold cursor-pointer transition-all duration-200 border border-text-tertiary text-text-secondary bg-transparent hover:bg-white/10 hover:border-text-secondary focus-visible:outline-3 focus-visible:outline-focus focus-visible:outline-offset-2"
                  onClick={handleLogout}
                >
                  Logout
                </button>
              </>
            ) : (
              <button
                className="px-5 py-2 rounded-lg text-sm font-semibold cursor-pointer transition-all duration-200 border-none bg-linear-to-br from-accent-start to-accent-end text-white hover:-translate-y-0.5 hover:shadow-lg hover:shadow-accent-start/40 focus-visible:outline-3 focus-visible:outline-focus focus-visible:outline-offset-2"
                onClick={handleLogin}
              >
                Login
              </button>
            )}
          </div>
        </div>
        <h1 className="text-4xl font-bold mt-5 mb-2 bg-linear-to-br from-text-primary to-text-secondary bg-clip-text text-transparent tracking-tight max-md:text-2xl">
          Aspire Starter
        </h1>
        <p className="text-base text-text-tertiary m-0 font-light max-md:text-sm">
          Modern distributed application development with Duende BFF
        </p>
      </header>

      {/* Main Content */}
      <main className="flex-1 max-w-[1400px] w-full mx-auto px-8 pb-8 flex justify-center items-center max-md:px-3">
        <section className="animate-fade-in-up flex-1 max-w-[1200px] w-full" aria-labelledby="weather-heading">
          {/* Card */}
          <div className="bg-card-bg backdrop-blur-md rounded-2xl px-6 py-5 shadow-lg border border-weather-card-border flex flex-col gap-4 max-md:px-4 max-md:py-4">
            {/* Section Header */}
            <div className="flex justify-between items-start flex-wrap gap-4 max-md:flex-col max-md:items-stretch">
              <div className="flex items-center gap-3">
                <h2 id="weather-heading" className="text-xl font-semibold m-0 text-section-title max-md:text-lg">
                  Weather Forecast
                </h2>
                {auth.isLoggedIn && (
                  <button
                    className={`px-3 py-1 rounded-md text-xs font-medium transition-all duration-200 ${
                      showSecure
                        ? 'bg-green-500/20 text-green-400 border border-green-500/50'
                        : 'bg-white/10 text-text-secondary border border-text-tertiary hover:bg-white/20'
                    }`}
                    onClick={() => setShowSecure(!showSecure)}
                  >
                    {showSecure ? 'Secure Mode' : 'Public Mode'}
                  </button>
                )}
              </div>
              <div className="flex gap-3 items-center max-md:w-full">
                {/* Temperature Toggle */}
                <fieldset
                  className="flex bg-white/10 dark:bg-white/10 light:bg-accent-start/8 rounded-lg p-1 gap-1 border border-weather-card-border m-0 max-md:flex-1"
                  aria-label="Temperature unit selection"
                >
                  <legend className="visually-hidden">Temperature unit</legend>
                  <button
                    className={`flex items-center justify-center bg-transparent border-none rounded-md px-4 h-10 text-sm font-semibold cursor-pointer transition-all duration-300 min-w-12 max-md:flex-1 ${
                      !useCelsius
                        ? 'bg-linear-to-br from-accent-start to-accent-end text-white shadow-md shadow-accent-start/30'
                        : 'text-text-secondary hover:bg-white/5 hover:text-text-primary'
                    } focus-visible:outline-3 focus-visible:outline-focus focus-visible:outline-offset-2 focus-visible:z-10`}
                    onClick={() => setUseCelsius(false)}
                    aria-pressed={!useCelsius}
                    type="button"
                  >
                    <span aria-hidden="true">°F</span>
                    <span className="visually-hidden">Fahrenheit</span>
                  </button>
                  <button
                    className={`flex items-center justify-center bg-transparent border-none rounded-md px-4 h-10 text-sm font-semibold cursor-pointer transition-all duration-300 min-w-12 max-md:flex-1 ${
                      useCelsius
                        ? 'bg-linear-to-br from-accent-start to-accent-end text-white shadow-md shadow-accent-start/30'
                        : 'text-text-secondary hover:bg-white/5 hover:text-text-primary'
                    } focus-visible:outline-3 focus-visible:outline-focus focus-visible:outline-offset-2 focus-visible:z-10`}
                    onClick={() => setUseCelsius(true)}
                    aria-pressed={useCelsius}
                    type="button"
                  >
                    <span aria-hidden="true">°C</span>
                    <span className="visually-hidden">Celsius</span>
                  </button>
                </fieldset>

                {/* Refresh Button */}
                <button
                  className="flex items-center justify-center gap-2 bg-linear-to-br from-accent-start to-accent-end text-white border-none rounded-lg px-6 h-12 text-sm font-semibold cursor-pointer transition-all duration-300 shadow-md shadow-accent-start/30 min-w-[140px] whitespace-nowrap hover:not-disabled:-translate-y-0.5 hover:not-disabled:shadow-lg hover:not-disabled:shadow-accent-start/40 disabled:opacity-60 disabled:cursor-not-allowed focus-visible:outline-3 focus-visible:outline-focus focus-visible:outline-offset-3 max-md:flex-1 max-md:px-5 max-md:h-11"
                  onClick={handleRefresh}
                  disabled={isCurrentlyFetching}
                  aria-label={isCurrentlyFetching ? 'Loading weather forecast' : 'Refresh weather forecast'}
                  type="button"
                >
                  <svg
                    className={`transition-transform duration-300 ${isCurrentlyFetching ? 'animate-spin' : ''}`}
                    width="20"
                    height="20"
                    viewBox="0 0 24 24"
                    fill="none"
                    stroke="currentColor"
                    strokeWidth="2"
                    aria-hidden="true"
                    focusable="false"
                  >
                    <path d="M21.5 2v6h-6M2.5 22v-6h6M2 11.5a10 10 0 0 1 18.8-4.3M22 12.5a10 10 0 0 1-18.8 4.2" />
                  </svg>
                  <span>{isCurrentlyFetching ? 'Loading...' : 'Refresh'}</span>
                </button>
              </div>
            </div>

            {/* Error Message */}
            {(error || secureError) && (
              <div
                className="flex items-center gap-3 bg-error-bg border-l-4 border-error-border text-error-text p-4 rounded-lg my-4 animate-slide-in"
                role="alert"
                aria-live="polite"
              >
                <svg
                  width="20"
                  height="20"
                  viewBox="0 0 24 24"
                  fill="none"
                  stroke="currentColor"
                  strokeWidth="2"
                  aria-hidden="true"
                >
                  <circle cx="12" cy="12" r="10" />
                  <line x1="12" y1="8" x2="12" y2="12" />
                  <line x1="12" y1="16" x2="12.01" y2="16" />
                </svg>
                <span>{(error || secureError) instanceof Error ? (error || secureError)?.message : 'Failed to fetch weather data'}</span>
              </div>
            )}

            {/* Loading Skeleton */}
            {(loading || (showSecure && secureLoading)) && weatherData.length === 0 && secureWeatherData.length === 0 && (
              <div className="flex flex-col gap-4 mt-4" role="status" aria-live="polite" aria-label="Loading weather data">
                {[...Array(5)].map((_, i) => (
                  <div
                    key={i}
                    className="h-20 bg-linear-to-r from-skeleton-1 via-skeleton-2 to-skeleton-1 bg-[length:200%_100%] animate-shimmer rounded-lg"
                    aria-hidden="true"
                  />
                ))}
                <span className="visually-hidden">Loading weather forecast data...</span>
              </div>
            )}

            {/* Weather Grid */}
            {(() => {
              const displayData = showSecure && auth.isLoggedIn ? secureWeatherData : weatherData
              const isSecureData = showSecure && auth.isLoggedIn && secureWeatherData.length > 0

              return displayData.length > 0 && (
                <div className="grid grid-cols-[repeat(auto-fill,minmax(200px,1fr))] gap-4 mt-3 max-md:grid-cols-1 max-md:gap-3">
                  {displayData.map((forecast, index) => (
                    <article
                      key={index}
                      className={`bg-weather-card-bg rounded-xl p-5 flex flex-col gap-3 transition-all duration-300 border backdrop-blur-md min-h-[120px] hover:-translate-y-1 hover:shadow-lg hover:shadow-black/20 focus-within:outline-2 focus-within:outline-focus focus-within:outline-offset-2 ${
                        isSecureData ? 'border-green-500/30' : 'border-weather-card-border'
                      }`}
                      aria-label={`Weather for ${formatDate(forecast.date)}`}
                    >
                      <h3 className="font-semibold text-sm text-date uppercase tracking-wide m-0">
                        <time dateTime={forecast.date}>{formatDate(forecast.date)}</time>
                      </h3>
                      <p className="text-lg font-medium text-summary min-h-6 m-0">{forecast.summary}</p>
                      {'requestedBy' in forecast && (
                        <p className="text-xs text-green-400 m-0">
                          Requested by: {forecast.requestedBy}
                        </p>
                      )}
                      <div
                        className="flex items-center justify-center gap-3 mt-2 pt-3 border-t border-weather-card-border"
                        aria-label={`Temperature: ${useCelsius ? forecast.temperatureC : forecast.temperatureF} degrees ${useCelsius ? 'Celsius' : 'Fahrenheit'}`}
                      >
                        <div className="flex flex-col items-center w-full">
                          <span
                            className="text-2xl font-bold bg-linear-to-br from-accent-start to-accent-end bg-clip-text text-transparent"
                            aria-hidden="true"
                          >
                            {useCelsius ? forecast.temperatureC : forecast.temperatureF}°
                          </span>
                          <span className="text-xs text-temp-unit mt-0.5" aria-hidden="true">
                            {useCelsius ? 'Celsius' : 'Fahrenheit'}
                          </span>
                        </div>
                      </div>
                    </article>
                  ))}
                </div>
              )
            })()}
          </div>
        </section>
      </main>

      {/* Footer */}
      <footer className="py-6 text-center bg-black/20 backdrop-blur-md flex justify-center items-center max-md:py-4">
        <nav
          className="max-w-[1400px] w-full flex justify-between items-center px-8 gap-4 max-md:flex-col max-md:px-6"
          aria-label="Footer navigation"
        >
          <a
            href="https://aspire.dev"
            target="_blank"
            rel="noopener noreferrer"
            className="text-text-secondary no-underline font-medium transition-all duration-300 border-b-2 border-transparent text-sm pb-0.5 hover:text-text-primary hover:border-text-primary focus-visible:outline-3 focus-visible:outline-focus focus-visible:outline-offset-4 focus-visible:rounded"
          >
            Learn more about Aspire<span className="visually-hidden"> (opens in new tab)</span>
          </a>
          <a
            href="https://github.com/dotnet/aspire"
            target="_blank"
            rel="noopener noreferrer"
            className="github-link inline-flex items-center gap-2 border-b-0 focus-visible:outline-3 focus-visible:outline-focus focus-visible:outline-offset-4 focus-visible:rounded max-md:order-first"
            aria-label="View Aspire on GitHub (opens in new tab)"
          >
            <img
              src="/github.svg"
              alt=""
              width="24"
              height="24"
              aria-hidden="true"
              className="github-icon transition-transform duration-300 hover:scale-110"
            />
            <span className="visually-hidden">GitHub</span>
          </a>
        </nav>
      </footer>
    </div>
  )
}
