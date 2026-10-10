import { useState } from 'react';
import { Navigate, useLocation } from 'react-router-dom';
import { ApiError, toErrorMessage } from '../../api/client';
import { useAuth } from '../../auth/useAuth';
import { Field } from '../../components/Field';

interface LoginLocationState {
  from?: string;
}

type LoginErrors = Partial<Record<'email' | 'password', string>>;

export function LoginPage() {
  const { isAuthenticated, login } = useAuth();
  const location = useLocation();

  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [errors, setErrors] = useState<LoginErrors>({});
  const [isSaving, setIsSaving] = useState(false);
  const [submitError, setSubmitError] = useState<string>();

  // Back to the page the visitor wanted, or the ticket list.
  const from = (location.state as LoginLocationState | null)?.from ?? '/tickets';

  if (isAuthenticated) {
    return <Navigate to={from} replace />;
  }

  const submit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    const validationErrors = validate(email, password);
    setErrors(validationErrors);
    setSubmitError(undefined);

    if (Object.keys(validationErrors).length > 0) {
      return;
    }

    setIsSaving(true);

    try {
      await login(email.trim(), password);
    } catch (caught) {
      // The API gives one message for every kind of rejection; show it without guessing which part was wrong.
      setSubmitError(
        caught instanceof ApiError && caught.status === 401
          ? 'Invalid email or password.'
          : toErrorMessage(caught, 'Could not sign in.'),
      );
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <main className="app__main">
      <section>
        <header className="page-header">
          <div>
            <h1>Sign in</h1>
            <p className="page-header__subtitle">Northwind Support</p>
          </div>
        </header>

        <form className="card form" onSubmit={submit} noValidate>
          <Field id="login-email" label="Email" error={errors.email}>
            {(fieldProps) => (
              <input
                {...fieldProps}
                type="email"
                autoComplete="username"
                value={email}
                onChange={(event) => setEmail(event.target.value)}
              />
            )}
          </Field>

          <Field id="login-password" label="Password" error={errors.password}>
            {(fieldProps) => (
              <input
                {...fieldProps}
                type="password"
                autoComplete="current-password"
                value={password}
                onChange={(event) => setPassword(event.target.value)}
              />
            )}
          </Field>

          {submitError && (
            <p className="field__error" role="alert">
              {submitError}
            </p>
          )}

          <div className="button-row">
            <button type="submit" className="button button--primary" disabled={isSaving}>
              {isSaving ? 'Signing in...' : 'Sign in'}
            </button>
          </div>
        </form>
      </section>
    </main>
  );
}

function validate(email: string, password: string): LoginErrors {
  const errors: LoginErrors = {};

  if (email.trim().length === 0) {
    errors.email = 'Enter your email.';
  }

  if (password.length === 0) {
    errors.password = 'Enter your password.';
  }

  return errors;
}